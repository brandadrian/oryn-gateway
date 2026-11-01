using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ShopwareLauncher.Docker;

public sealed class DockerException(string message, int? statusCode = null) : Exception(message)
{
    public int? StatusCode { get; } = statusCode;
}

/// <summary>Minimaler Client fuer die Docker Engine API ueber den Unix-Socket (ohne NuGet-Pakete).</summary>
public sealed partial class DockerClient(HttpClient http)
{
    // Docker erwartet PascalCase -> keine Naming-Policy
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    /// <summary>Konfiguriert einen HttpMessageHandler, der ueber den Unix-Socket verbindet.</summary>
    public static SocketsHttpHandler CreateHandler(string socketPath) => new()
    {
        ConnectCallback = async (_, ct) =>
        {
            var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            try
            {
                await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), ct);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        },
    };

    public async Task<IReadOnlyList<ContainerSummary>> ListContainersAsync(string labelFilter, CancellationToken ct = default)
    {
        var filters = Uri.EscapeDataString(JsonSerializer.Serialize(new { label = new[] { labelFilter } }));
        return await GetJsonAsync<List<ContainerSummary>>($"/containers/json?all=1&filters={filters}", ct);
    }

    public Task<ContainerInspect> InspectContainerAsync(string id, CancellationToken ct = default) =>
        GetJsonAsync<ContainerInspect>($"/containers/{id}/json", ct);

    public async Task<string> CreateContainerAsync(string name, CreateContainerRequest spec, CancellationToken ct = default)
    {
        var res = await SendAsync(HttpMethod.Post, $"/containers/create?name={Uri.EscapeDataString(name)}", spec, ct);
        return (await res.Content.ReadFromJsonAsync<IdResponse>(Json, ct))!.Id;
    }

    public Task StartContainerAsync(string id, CancellationToken ct = default) =>
        SendAsync(HttpMethod.Post, $"/containers/{id}/start", null, ct);

    public Task RemoveContainerAsync(string id, CancellationToken ct = default) =>
        SendAsync(HttpMethod.Delete, $"/containers/{id}?force=1&v=1", null, ct);

    public async Task<string> GetLogsAsync(string id, DateTimeOffset since, CancellationToken ct = default)
    {
        var res = await SendAsync(HttpMethod.Get, $"/containers/{id}/logs?stdout=1&stderr=1&since={since.ToUnixTimeSeconds()}", null, ct);
        return await res.Content.ReadAsStringAsync(ct);
    }

    /// <summary>Image laden. Die Antwort ist ein JSON-Stream, Fehler stehen als {"error": ...} darin.</summary>
    public async Task PullImageAsync(string image, string tag, CancellationToken ct = default)
    {
        var res = await SendAsync(HttpMethod.Post,
            $"/images/create?fromImage={Uri.EscapeDataString(image)}&tag={Uri.EscapeDataString(tag)}", null, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        foreach (var line in body.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            PullProgress? progress;
            try { progress = JsonSerializer.Deserialize<PullProgress>(line, Json); }
            catch (JsonException) { continue; }
            if (progress?.Error is { } error) throw new DockerException(error);
        }
    }

    /// <summary>Befehl im Container ausfuehren und auf das Ende warten.</summary>
    public async Task<ExecResult> ExecAsync(string id, IReadOnlyList<string> cmd, string user = "root", CancellationToken ct = default)
    {
        var create = await SendAsync(HttpMethod.Post, $"/containers/{id}/exec", new ExecRequest(cmd, user), ct);
        var execId = (await create.Content.ReadFromJsonAsync<IdResponse>(Json, ct))!.Id;

        var start = await SendAsync(HttpMethod.Post, $"/exec/{execId}/start", new ExecStartRequest(), ct);
        var output = await start.Content.ReadAsStringAsync(ct);

        var info = await GetJsonAsync<ExecInspect>($"/exec/{execId}/json", ct);
        // Stream-Header (8 Byte pro Frame) grob entfernen
        return new ExecResult(info.ExitCode, StreamHeaderChars().Replace(output, ""));
    }

    private async Task<T> GetJsonAsync<T>(string path, CancellationToken ct)
    {
        var res = await SendAsync(HttpMethod.Get, path, null, ct);
        return (await res.Content.ReadFromJsonAsync<T>(Json, ct))!;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(method, path);
        if (body is not null)
            req.Content = new StringContent(JsonSerializer.Serialize(body, body.GetType(), Json), Encoding.UTF8, "application/json");

        var res = await http.SendAsync(req, ct);
        if (res.IsSuccessStatusCode) return res;

        var text = await res.Content.ReadAsStringAsync(ct);
        try { text = JsonDocument.Parse(text).RootElement.GetProperty("message").GetString() ?? text; }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException) { }
        throw new DockerException($"Docker {method} {path}: {(int)res.StatusCode} {text}", (int)res.StatusCode);
    }

    [GeneratedRegex(@"[\x00-\x08]")]
    private static partial Regex StreamHeaderChars();
}
