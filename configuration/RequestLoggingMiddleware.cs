using System.Diagnostics;

namespace configuration;

public class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    private const string RequestIdHeader = "X-Request-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        string requestId = context.Request.Headers.TryGetValue(RequestIdHeader, out var incoming) && !string.IsNullOrWhiteSpace(incoming)
            ? incoming.ToString()
            : Activity.Current?.Id ?? Guid.NewGuid().ToString("N");
        context.Response.Headers[RequestIdHeader] = requestId;

        var routeValues = context.Request.RouteValues;
        string? sessionId = null;
        if (routeValues.TryGetValue("id", out object? value) && value is not null)
        {
            sessionId = value.ToString();
        }

        var state = new Dictionary<string, object>
        {
            ["RequestId"] = requestId,
            ["Method"] = context.Request.Method,
            ["Path"] = context.Request.Path.Value ?? string.Empty
        };
        if (sessionId is not null) state["SessionId"] = sessionId;

        Stopwatch stopwatch = Stopwatch.StartNew();
        using (logger.BeginScope(state))
        {
            try
            {
                await next(context);
            }
            finally
            {
                stopwatch.Stop();
                int status = context.Response.StatusCode;
                if (status >= 500)
                {
                    logger.LogError("Request {Method} {Path} completed in {ElapsedMs}ms with {StatusCode}",
                        context.Request.Method, context.Request.Path, stopwatch.ElapsedMilliseconds, status);
                }
                else if (status >= 400)
                {
                    logger.LogWarning("Request {Method} {Path} completed in {ElapsedMs}ms with {StatusCode}",
                        context.Request.Method, context.Request.Path, stopwatch.ElapsedMilliseconds, status);
                }
                else
                {
                    logger.LogInformation("Request {Method} {Path} completed in {ElapsedMs}ms with {StatusCode}",
                        context.Request.Method, context.Request.Path, stopwatch.ElapsedMilliseconds, status);
                }
            }
        }
    }
}
