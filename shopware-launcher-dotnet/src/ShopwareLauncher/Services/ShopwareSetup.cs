using ShopwareLauncher.Docker;

namespace ShopwareLauncher.Services;

public sealed record SetupParameters(string PublicHost, string LocalHost, string AdminPassword);

/// <summary>Nachbereitung eines dockware-Containers fuer den Betrieb hinter Traefik.</summary>
public sealed class ShopwareSetup(DockerClient docker)
{
    private const string ReadyMarker = "SHOP URL"; // gibt dockware am Ende des Entrypoints aus
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(20);

    public async Task RunAsync(string containerId, SetupParameters p, CancellationToken ct)
    {
        await WaitForDockwareAsync(containerId, ct);
        var result = await docker.ExecAsync(containerId, ["bash", "-c", BuildScript(p)], ct: ct);
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"Setup fehlgeschlagen (Exit {result.ExitCode}): {Tail(result.Output, 500)}");
    }

    /// <summary>Wartet, bis dockware seit dem letzten Containerstart fertig initialisiert ist.</summary>
    private async Task WaitForDockwareAsync(string containerId, CancellationToken ct)
    {
        var since = (await docker.InspectContainerAsync(containerId, ct)).State.StartedAt;
        var deadline = DateTimeOffset.UtcNow + Timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if ((await docker.GetLogsAsync(containerId, since, ct)).Contains(ReadyMarker)) return;
            await Task.Delay(PollInterval, ct);
        }
        throw new TimeoutException("Timeout: dockware ist nicht fertig geworden");
    }

    /// <summary>
    /// Bash-Skript (als root im Container):
    /// Apache meldet HTTPS an PHP (X-Forwarded-Proto), APP_URL auf https, Admin-Passwort,
    /// zusaetzliche Sales-Channel-Domain fuer https://&lt;local&gt; (nicht kritisch).
    /// </summary>
    public static string BuildScript(SetupParameters p) => $"""
        set -e
        echo 'SetEnvIf X-Forwarded-Proto "https" HTTPS=on' > /etc/apache2/conf-enabled/zz-sw-launcher.conf
        sed -i 's#^APP_URL=http://#APP_URL=https://#' /var/www/html/.env
        chown www-data:www-data /var/www/html/.env
        cd /var/www/html
        su -s /bin/bash www-data -c "php bin/console user:change-password admin --password='{p.AdminPassword}'"
        MYSQL_PWD=root mysql -h 127.0.0.1 -uroot shopware -e "
          SET SESSION sql_mode = '';
          DELETE FROM sales_channel_domain WHERE url = 'https://{p.LocalHost}';
          CREATE TEMPORARY TABLE t AS SELECT * FROM sales_channel_domain WHERE url = 'https://{p.PublicHost}' LIMIT 1;
          UPDATE t SET id = UNHEX(REPLACE(UUID(), '-', '')), url = 'https://{p.LocalHost}', created_at = NOW(3);
          INSERT INTO sales_channel_domain SELECT * FROM t;" || echo "WARNUNG: lokale Domain konnte nicht angelegt werden"
        su -s /bin/bash www-data -c "php bin/console cache:clear"
        apache2ctl -k graceful
        """;

    private static string Tail(string s, int max) => s.Length <= max ? s : s[^max..];
}
