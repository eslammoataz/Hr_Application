using Microsoft.Extensions.Primitives;
using Serilog.Context;

namespace HrSystemApp.Api.Middleware;

/// <summary>
/// Middleware that assigns a unique correlation ID to each HTTP request if not already present.
/// The ID is added to response headers and Serilog's LogContext for structured logging.
/// </summary>
public class CorrelationIdMiddleware
{
    private const string CorrelationIdHeader = "X-Correlation-ID";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue(CorrelationIdHeader, out StringValues correlationId))
        {
            correlationId = Guid.NewGuid().ToString();
        }

        context.Response.Headers[CorrelationIdHeader] = correlationId;

        // Add to Serilog LogContext so all logs within this request have the ID
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }
}
