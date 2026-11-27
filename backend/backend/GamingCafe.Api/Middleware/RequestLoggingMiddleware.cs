using System.Diagnostics;

namespace GamingCafe.Api.Middleware;

public sealed class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate _next, ILogger<RequestLoggingMiddleware> logger)
    {
        this._next = _next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        // Skip noisy health-check and OpenAPI swagger docs from detailed HTTP logs
        if (path.Equals("/health", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/openapi", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var correlationId = context.TraceIdentifier;
        var method = context.Request.Method;
        var stopwatch = Stopwatch.StartNew();

        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["HttpMethod"] = method,
            ["RequestPath"] = path
        }))
        {
            try
            {
                await _next(context);
            }
            finally
            {
                stopwatch.Stop();
                var statusCode = context.Response.StatusCode;
                var elapsedMs = stopwatch.Elapsed.TotalMilliseconds;

                if (statusCode >= 500)
                {
                    _logger.LogError("HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs:F2} ms [CorrelationId: {CorrelationId}]",
                        method, path, statusCode, elapsedMs, correlationId);
                }
                else if (statusCode >= 400)
                {
                    _logger.LogWarning("HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs:F2} ms [CorrelationId: {CorrelationId}]",
                        method, path, statusCode, elapsedMs, correlationId);
                }
                else
                {
                    _logger.LogInformation("HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs:F2} ms [CorrelationId: {CorrelationId}]",
                        method, path, statusCode, elapsedMs, correlationId);
                }
            }
        }
    }
}
