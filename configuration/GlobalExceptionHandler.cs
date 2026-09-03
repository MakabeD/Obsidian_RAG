using Microsoft.AspNetCore.Diagnostics;
using vaultReader;

namespace configuration;

public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        (int status, string title) = exception switch
        {
            NotSupportedException => (StatusCodes.Status400BadRequest, "Unsupported file type"),
            UnsafeZipException    => (StatusCodes.Status400BadRequest, "Unsafe or oversized zip archive"),
            ArgumentException       => (StatusCodes.Status400BadRequest, "Invalid argument"),
            BadHttpRequestException => (StatusCodes.Status400BadRequest, "Invalid request"),
            HttpRequestException    => (StatusCodes.Status502BadGateway, "Error communicating with an external service"),
            _                       => (StatusCodes.Status500InternalServerError, "Internal server error")
        };

        if (status == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception");
        }
        else
        {
            logger.LogWarning(exception, "Request rejected with {Status}", status);
        }

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(new
        {
            type = $"https://httpstatuses.io/{status}",
            title,
            status,
            detail = exception.Message,
            traceId = httpContext.TraceIdentifier
        }, cancellationToken);

        return true;
    }
}
