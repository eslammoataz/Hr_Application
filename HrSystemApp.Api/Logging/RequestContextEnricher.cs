using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Serilog.Core;
using Serilog.Events;

namespace HrSystemApp.Api.Logging;

/// <summary>
/// A Serilog enricher that lazily reads HTTP request context at log-emit time.
///
/// Why a Serilog enricher instead of middleware (LoggingScopeMiddleware)?
///
/// Middleware-based enrichment via LogContext.PushProperty only covers logs emitted
/// inside the middleware scope. Three categories of logs escape that scope:
///   1. "Request starting" — emitted by Kestrel BEFORE the middleware pipeline runs.
///   2. "Request finished" — emitted by Kestrel AFTER the middleware pipeline completes.
///   3. Startup/shutdown/Hangfire logs — no HttpContext at all.
///
/// This enricher is called by Serilog at the moment each log event is written, so it
/// always reads the freshest available context from IHttpContextAccessor. Non-HTTP logs
/// (startup, shutdown, background jobs) get empty-string values so the output template
/// renders cleanly without hanging brackets.
///
/// CorrelationId remains owned by CorrelationIdMiddleware (via LogContext.PushProperty)
/// for in-flight HTTP logs. This enricher provides a response-header fallback for logs
/// that escape the middleware scope (e.g. "Request finished"), and an empty-string
/// default for non-HTTP logs. The AddPropertyIfAbsent guard ensures the middleware value
/// always wins when it is present.
/// </summary>
public sealed class RequestContextEnricher : ILogEventEnricher
{
    private const string CorrelationIdHeader = "X-Correlation-ID";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IWebHostEnvironment _environment;

    public RequestContextEnricher(IHttpContextAccessor httpContextAccessor, IWebHostEnvironment environment)
    {
        _httpContextAccessor = httpContextAccessor;
        _environment = environment;
    }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        // AppEnvironment is always available — not request-scoped.
        logEvent.AddPropertyIfAbsent(
            propertyFactory.CreateProperty("AppEnvironment", _environment.EnvironmentName));

        var ctx = _httpContextAccessor.HttpContext;

        if (ctx is null)
        {
            // Non-HTTP context (startup, shutdown, background jobs).
            // Emit empty strings so the output template renders cleanly: [Corr=] [User=] etc.
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("CorrelationId", string.Empty));
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("UserId", string.Empty));
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("Email", string.Empty));
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("TraceId", string.Empty));
            return;
        }

        // ── CorrelationId ─────────────────────────────────────────────────────────
        // Priority: LogContext (set by CorrelationIdMiddleware) > response header > empty.
        // AddPropertyIfAbsent ensures the LogContext value wins for in-pipeline logs.
        // The response-header fallback covers "Request finished" which runs after middleware.
        if (!logEvent.Properties.ContainsKey("CorrelationId"))
        {
            var correlationId = ctx.Response.Headers.TryGetValue(CorrelationIdHeader, out var header)
                ? header.ToString()
                : string.Empty;
            logEvent.AddPropertyIfAbsent(
                propertyFactory.CreateProperty("CorrelationId", correlationId));
        }

        // ── TraceId ───────────────────────────────────────────────────────────────
        var traceId = Activity.Current?.TraceId.ToString() ?? ctx.TraceIdentifier;
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("TraceId", traceId));

        // ── UserId ────────────────────────────────────────────────────────────────
        // Read lazily at emit time — correct even for logs emitted before auth has run
        // (they will show "Anonymous") and for logs emitted after (they show the real ID).
        var userId = ctx.User?.FindFirst("sub")?.Value
            ?? ctx.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? "Anonymous";
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("UserId", userId));

        // ── Email ─────────────────────────────────────────────────────────────────
        var email = ctx.User?.FindFirst("email")?.Value
            ?? ctx.User?.FindFirst(ClaimTypes.Email)?.Value
            ?? string.Empty;
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("Email", email));
    }
}
