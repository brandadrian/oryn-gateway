namespace ShopwareLauncher.Infrastructure;

/// <summary>Fachlicher Fehler, der als {"error": "..."} mit Statuscode an den Client geht.</summary>
public sealed class ApiException(string message, int statusCode = StatusCodes.Status400BadRequest) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
