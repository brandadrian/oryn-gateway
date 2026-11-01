using Microsoft.Extensions.Options;
using ShopwareLauncher.Infrastructure;
using ShopwareLauncher.Options;
using ShopwareLauncher.Services;

namespace ShopwareLauncher.Api;

/// <summary>REST-API (gleicher Vertrag wie die JS-Version, damit das Frontend identisch bleibt).</summary>
public static class InstanceEndpoints
{
    public static IEndpointRouteBuilder MapLauncherApi(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/config", (IOptions<LauncherOptions> o) =>
            new ConfigResponse(o.Value.Domain, o.Value.MaxInstances, o.Value.AutoDeleteMinutes));

        api.MapGet("/versions", (VersionService versions, CancellationToken ct) => versions.ListAsync(ct));

        api.MapGet("/instances", (InstanceService instances, CancellationToken ct) => instances.ListAsync(ct));

        api.MapPost("/instances", async (CreateInstanceRequest req, InstanceService instances, VersionService versions, CancellationToken ct) =>
        {
            if (req.Version is null || !(await versions.ListAsync(ct)).Contains(req.Version))
                throw new ApiException("Unbekannte Version");
            var created = await instances.CreateAsync(req.Name, req.Version, req.AutoDelete, ct);
            return Results.Accepted(value: created);
        });

        api.MapDelete("/instances/{name:regex(^[a-z0-9-]+$)}", async (string name, InstanceService instances, CancellationToken ct) =>
        {
            await instances.RemoveAsync(name, ct);
            return Results.Ok(new { ok = true });
        });

        return app;
    }
}
