using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using ShopwareLauncher.Api;
using ShopwareLauncher.Docker;
using ShopwareLauncher.Infrastructure;
using ShopwareLauncher.Options;

namespace ShopwareLauncher.Services;

/// <summary>
/// Lebenszyklus der Shopware-Instanzen: anlegen, auflisten, loeschen, Auto-Loeschen.
/// Der dauerhafte Zustand steckt in Docker-Labels; nur laufende Erstellungen sind im Speicher.
/// </summary>
public sealed partial class InstanceService(
    DockerClient docker,
    ShopwareSetup setup,
    IOptions<LauncherOptions> options,
    IHostApplicationLifetime lifetime,
    ILogger<InstanceService> logger)
{
    private static class Label
    {
        public const string Marker = "sw-launcher";
        public const string Name = "sw-launcher.name";
        public const string Version = "sw-launcher.version";
        public const string Expires = "sw-launcher.expires";
        public const string SshPort = "sw-launcher.ssh-port";
        public const string SshUser = "sw-launcher.ssh-user";
        public const string SshPassword = "sw-launcher.ssh-password";
        public const string AdminPassword = "sw-launcher.admin-password";
    }

    private const string SshUser = "dev";
    private const string AdminUser = "admin";

    /// <summary>Instanzen, die gerade erstellt oder eingerichtet werden.</summary>
    private sealed class Pending(string version, string? expires, int sshPort)
    {
        public string Step { get; set; } = "pulling";
        public string? Error { get; set; }
        public string Version { get; } = version;
        public string? Expires { get; } = expires;
        public int SshPort { get; } = sshPort;
    }

    private readonly LauncherOptions _options = options.Value;
    private readonly ConcurrentDictionary<string, Pending> _pending = new();
    private readonly SemaphoreSlim _createLock = new(1, 1);

    public string PublicHost(string name) => $"sw-{name}.{_options.Domain}";
    public static string LocalHost(string name) => $"sw-{name}.localhost";

    private Task<IReadOnlyList<ContainerSummary>> ContainersAsync(CancellationToken ct) =>
        docker.ListContainersAsync($"{Label.Marker}=1", ct);

    // ---------------------------------------------------------------- Abfragen
    public async Task<IReadOnlyList<InstanceDto>> ListAsync(CancellationToken ct = default)
    {
        var items = (await ContainersAsync(ct)).Select(ToDto).ToList();
        foreach (var (name, p) in _pending)
        {
            if (items.Any(i => i.Name == name)) continue;
            items.Add(new InstanceDto { Name = name, Version = p.Version, Status = StatusOf(p), Error = p.Error, Expires = p.Expires });
        }
        return [.. items.OrderBy(i => i.Name, StringComparer.Ordinal)];
    }

    private InstanceDto ToDto(ContainerSummary c)
    {
        var l = c.Labels;
        var name = l[Label.Name];
        _pending.TryGetValue(name, out var pending);
        return new InstanceDto
        {
            Name = name,
            Version = l.GetValueOrDefault(Label.Version),
            State = c.State,
            Status = pending is not null ? StatusOf(pending) : c.State == "running" ? "ready" : c.State,
            Error = pending?.Error,
            Created = DateTimeOffset.FromUnixTimeSeconds(c.Created),
            Expires = NullIfEmpty(l.GetValueOrDefault(Label.Expires)),
            Url = $"https://{PublicHost(name)}",
            LocalUrl = $"https://{LocalHost(name)}",
            AdminUser = AdminUser,
            AdminPassword = l.GetValueOrDefault(Label.AdminPassword),
            SshHost = PublicHost(name),
            SshPort = int.TryParse(l.GetValueOrDefault(Label.SshPort), out var port) ? port : null,
            SshUser = l.GetValueOrDefault(Label.SshUser),
            SshPassword = l.GetValueOrDefault(Label.SshPassword),
        };
    }

    private static string StatusOf(Pending p) => p.Error is not null ? "error" : p.Step;
    private static string? NullIfEmpty(string? s) => string.IsNullOrEmpty(s) ? null : s;

    // ---------------------------------------------------------------- Anlegen
    /// <summary>
    /// Prueft Limits, reserviert Name + SSH-Port und startet die Erstellung im Hintergrund.
    /// Der Name bekommt eine GUID als Suffix (eindeutig, URL nicht erratbar; max. 60 Zeichen &lt; DNS-Limit 63).
    /// </summary>
    public async Task<CreateInstanceResponse> CreateAsync(string? baseName, string version, bool autoDelete, CancellationToken ct = default)
    {
        if (baseName is null || !NameRegex().IsMatch(baseName))
            throw new ApiException("Name: nur a-z, 0-9, -, max. 20 Zeichen");

        string name;
        Pending pending;
        await _createLock.WaitAsync(ct);
        try
        {
            if ((await ListAsync(ct)).Count >= _options.MaxInstances)
                throw new ApiException($"Maximal {_options.MaxInstances} Instanzen", StatusCodes.Status409Conflict);

            name = $"{baseName}-{Guid.NewGuid()}";
            var expires = autoDelete ? DateTimeOffset.UtcNow.AddMinutes(_options.AutoDeleteMinutes).ToString("O") : null;
            pending = new Pending(version, expires, await FreeSshPortAsync(ct));
            _pending[name] = pending;
        }
        finally
        {
            _createLock.Release();
        }

        // Laeuft im Hintergrund weiter, auch wenn der HTTP-Request endet
        _ = Task.Run(async () =>
        {
            try
            {
                await ProvisionAsync(name, pending, lifetime.ApplicationStopping);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Erstellen von {Name} fehlgeschlagen", name);
                pending.Error = e.Message;
            }
        });

        return new CreateInstanceResponse(name);
    }

    private async Task ProvisionAsync(string name, Pending pending, CancellationToken ct)
    {
        await docker.PullImageAsync(_options.Image, pending.Version, ct);

        pending.Step = "starting";
        var adminPassword = RandomPassword();
        var sshPassword = RandomPassword();
        var publicHost = PublicHost(name);
        var localHost = LocalHost(name);

        var labels = new Dictionary<string, string>
        {
            [Label.Marker] = "1",
            [Label.Name] = name,
            [Label.Version] = pending.Version,
            [Label.Expires] = pending.Expires ?? "",
            [Label.SshPort] = pending.SshPort.ToString(),
            [Label.SshUser] = SshUser,
            [Label.SshPassword] = sshPassword,
            [Label.AdminPassword] = adminPassword,
        };
        foreach (var (k, v) in TraefikLabels.For($"sw-{name}", publicHost, localHost)) labels[k] = v;

        var id = await docker.CreateContainerAsync($"sw-{name}", new CreateContainerRequest(
            Image: $"{_options.Image}:{pending.Version}",
            Env: [$"SHOP_DOMAIN={publicHost}", $"SSH_USER={SshUser}", $"SSH_PWD={sshPassword}"],
            Labels: labels,
            ExposedPorts: new Dictionary<string, object> { ["22/tcp"] = new() },
            HostConfig: new HostConfig(
                NetworkMode: _options.Network,
                PortBindings: new Dictionary<string, IReadOnlyList<PortBinding>>
                {
                    ["22/tcp"] = [new PortBinding(pending.SshPort.ToString())],
                },
                RestartPolicy: new RestartPolicy("unless-stopped"))), ct);
        await docker.StartContainerAsync(id, ct);

        pending.Step = "setup";
        await setup.RunAsync(id, new SetupParameters(publicHost, localHost, adminPassword), ct);
        _pending.TryRemove(name, out _);
    }

    private async Task<int> FreeSshPortAsync(CancellationToken ct)
    {
        var used = (await ContainersAsync(ct))
            .Select(c => int.TryParse(c.Labels.GetValueOrDefault(Label.SshPort), out var p) ? p : 0)
            .Concat(_pending.Values.Select(p => p.SshPort))
            .ToHashSet();

        foreach (var port in Enumerable.Range(_options.SshPortStart, _options.MaxInstances))
            if (!used.Contains(port)) return port;
        throw new ApiException("Kein freier SSH-Port", StatusCodes.Status409Conflict);
    }

    private static string RandomPassword() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(12)).Replace('+', '-').Replace('/', '_');

    // ---------------------------------------------------------------- Loeschen
    public async Task RemoveAsync(string name, CancellationToken ct = default)
    {
        _pending.TryRemove(name, out _);
        var container = (await ContainersAsync(ct)).FirstOrDefault(c => c.Labels.GetValueOrDefault(Label.Name) == name);
        if (container is not null) await docker.RemoveContainerAsync(container.Id, ct);
    }

    public async Task RemoveExpiredAsync(CancellationToken ct = default)
    {
        foreach (var c in await ContainersAsync(ct))
        {
            if (DateTimeOffset.TryParse(c.Labels.GetValueOrDefault(Label.Expires), out var expires) && expires < DateTimeOffset.UtcNow)
            {
                logger.LogInformation("Auto-Loeschen: {Name}", c.Labels.GetValueOrDefault(Label.Name));
                await docker.RemoveContainerAsync(c.Id, ct);
            }
        }
    }

    // ---------------------------------------------------------------- Neustart
    /// <summary>dockware setzt APP_URL bei jedem Containerstart zurueck -> Setup fuer laufende Instanzen erneut anwenden.</summary>
    public async Task ResumeRunningAsync(CancellationToken ct)
    {
        foreach (var c in await ContainersAsync(ct))
        {
            if (c.State != "running") continue;
            var name = c.Labels[Label.Name];
            var pending = new Pending(
                c.Labels.GetValueOrDefault(Label.Version) ?? "?",
                NullIfEmpty(c.Labels.GetValueOrDefault(Label.Expires)),
                int.TryParse(c.Labels.GetValueOrDefault(Label.SshPort), out var p) ? p : 0) { Step = "setup" };
            _pending[name] = pending;

            _ = Task.Run(async () =>
            {
                try
                {
                    await setup.RunAsync(c.Id, new SetupParameters(PublicHost(name), LocalHost(name),
                        c.Labels.GetValueOrDefault(Label.AdminPassword) ?? ""), ct);
                    _pending.TryRemove(name, out _);
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Setup nach Neustart fuer {Name} fehlgeschlagen", name);
                    pending.Error = e.Message;
                }
            }, ct);
        }
    }

    [GeneratedRegex("^[a-z0-9](?:[a-z0-9-]{0,18}[a-z0-9])?$")]
    private static partial Regex NameRegex();
}
