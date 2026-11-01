namespace ShopwareLauncher.Services;

/// <summary>Traefik-Labels fuer eine Instanz: oeffentlich (Let's Encrypt), lokal (*.localhost), Dev-Tools gesperrt.</summary>
public static class TraefikLabels
{
    public static Dictionary<string, string> For(string router, string publicHost, string localHost)
    {
        var r = router;
        var anyHost = $"Host(`{publicHost}`) || Host(`{localHost}`)";
        return new()
        {
            ["traefik.enable"] = "true",

            [$"traefik.http.routers.{r}.rule"] = $"Host(`{publicHost}`)",
            [$"traefik.http.routers.{r}.entrypoints"] = "websecure",
            [$"traefik.http.routers.{r}.tls.certresolver"] = "le",
            [$"traefik.http.routers.{r}.service"] = r,

            [$"traefik.http.routers.{r}-local.rule"] = $"Host(`{localHost}`)",
            [$"traefik.http.routers.{r}-local.entrypoints"] = "websecure",
            [$"traefik.http.routers.{r}-local.tls"] = "true",
            [$"traefik.http.routers.{r}-local.service"] = r,

            // dockware-Tools (Adminer mit root/root, Logs, Mailcatcher) -> immer 403
            [$"traefik.http.routers.{r}-block.rule"] =
                $"({anyHost}) && (PathPrefix(`/adminer`) || PathPrefix(`/logs`) || PathPrefix(`/mailcatcher`))",
            [$"traefik.http.routers.{r}-block.priority"] = "1000",
            [$"traefik.http.routers.{r}-block.entrypoints"] = "websecure",
            [$"traefik.http.routers.{r}-block.tls"] = "true",
            [$"traefik.http.routers.{r}-block.middlewares"] = $"{r}-deny",
            [$"traefik.http.routers.{r}-block.service"] = r,
            [$"traefik.http.middlewares.{r}-deny.ipallowlist.sourcerange"] = "255.255.255.255/32",

            [$"traefik.http.services.{r}.loadbalancer.server.port"] = "80",
        };
    }
}
