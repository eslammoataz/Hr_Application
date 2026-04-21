using System.Diagnostics;
using HrSystemApp.Application.Common.Logging;
using Microsoft.Extensions.Options;
using Serilog.Context;

namespace HrSystemApp.Api.Middleware;

public class RequestResponseLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestResponseLoggingMiddleware> _logger;
    private readonly LoggingOptions _loggingOptions;

    public RequestResponseLoggingMiddleware(
        RequestDelegate next,
        ILogger<RequestResponseLoggingMiddleware> logger,
        IOptions<LoggingOptions> loggingOptions)
    {
        _next = next;
        _logger = logger;
        _loggingOptions = loggingOptions.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        if (path.StartsWith("/health", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var sw = Stopwatch.StartNew();

        try
        {
            await _next(context);
        }
        finally
        {
            sw.Stop();
            LogRequest(context, path, sw.ElapsedMilliseconds);
        }
    }

    private void LogRequest(HttpContext context, string path, long elapsedMs)
    {
        if (!_loggingOptions.EnableRequestResponseLogging)
            return;

        var userId = context.User?.FindFirst("sub")?.Value
                  ?? context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                  ?? "Anonymous";

        var email = context.User?.FindFirst("email")?.Value
                 ?? context.User?.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;

        var statusCode = context.Response.StatusCode;
        var httpMethod = context.Request.Method;
        var isSlowRequest = elapsedMs >= _loggingOptions.SlowOperationThresholdMs;

        using (LogContext.PushProperty("UserId", userId))
        using (LogContext.PushProperty("Email", email))
        using (LogContext.PushProperty("StatusCode", statusCode))
        using (LogContext.PushProperty("ElapsedMs", elapsedMs))
        {
            if (isSlowRequest)
            {
                _logger.LogWarning(
                    "HttpRequest HTTP={HttpMethod} Path={RequestPath} StatusCode={StatusCode} ElapsedMs={ElapsedMs} — Slow request",
                    httpMethod, path, statusCode, elapsedMs);
            }
            else
            {
                _logger.LogInformation(
                    "HttpRequest HTTP={HttpMethod} Path={RequestPath} StatusCode={StatusCode} ElapsedMs={ElapsedMs}",
                    httpMethod, path, statusCode, elapsedMs);
            }
        }
    }
}
