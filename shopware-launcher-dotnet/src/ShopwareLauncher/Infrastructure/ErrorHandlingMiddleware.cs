using ShopwareLauncher.Api;

namespace ShopwareLauncher.Infrastructure;

/// <summary>Uebersetzt Exceptions in {"error": "..."} (gleiches Format wie die JS-Version).</summary>
public sealed class ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ApiException e)
        {
            await WriteAsync(context, e.StatusCode, e.Message);
        }
        catch (BadHttpRequestException)
        {
            await WriteAsync(context, StatusCodes.Status400BadRequest, "Ungueltige Anfrage");
        }
        catch (Exception e) when (!context.RequestAborted.IsCancellationRequested)
        {
            logger.LogError(e, "Unbehandelter Fehler bei {Path}", context.Request.Path);
            await WriteAsync(context, StatusCodes.Status500InternalServerError, e.Message);
        }
    }

    private static Task WriteAsync(HttpContext context, int status, string message)
    {
        if (context.Response.HasStarted) return Task.CompletedTask;
        context.Response.StatusCode = status;
        return context.Response.WriteAsJsonAsync(new ErrorResponse(message));
    }
}
