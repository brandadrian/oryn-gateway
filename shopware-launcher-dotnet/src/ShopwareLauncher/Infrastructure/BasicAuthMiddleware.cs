using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using ShopwareLauncher.Options;

namespace ShopwareLauncher.Infrastructure;

/// <summary>HTTP Basic Auth fuer die ganze App (UI + API), zeitkonstanter Vergleich.</summary>
public sealed class BasicAuthMiddleware(RequestDelegate next, IOptions<LauncherOptions> options)
{
    private readonly byte[] _userHash = Hash(options.Value.User);
    private readonly byte[] _passwordHash = Hash(options.Value.Password);

    public Task InvokeAsync(HttpContext context)
    {
        if (IsAuthorized(context.Request.Headers.Authorization.ToString())) return next(context);

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.Headers.WWWAuthenticate = "Basic realm=\"Shopware-Launcher\"";
        return context.Response.WriteAsync("Login erforderlich");
    }

    private bool IsAuthorized(string header)
    {
        if (!header.StartsWith("Basic ", StringComparison.Ordinal)) return false;

        string decoded;
        try { decoded = Encoding.UTF8.GetString(Convert.FromBase64String(header[6..])); }
        catch (FormatException) { return false; }

        var sep = decoded.IndexOf(':');
        if (sep < 0) return false;

        // beide Vergleiche immer ausfuehren (kein Short-Circuit)
        var userOk = CryptographicOperations.FixedTimeEquals(Hash(decoded[..sep]), _userHash);
        var passOk = CryptographicOperations.FixedTimeEquals(Hash(decoded[(sep + 1)..]), _passwordHash);
        return userOk & passOk;
    }

    private static byte[] Hash(string value) => SHA256.HashData(Encoding.UTF8.GetBytes(value));
}
