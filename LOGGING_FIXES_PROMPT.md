# Logging Fixes — Instructions for Cheaper Model

You are a senior .NET engineer working on an ASP.NET Core 8 Web API project called HrSystemApp. The project uses Serilog for structured logging, MediatR with pipeline behaviors, and a Result pattern (handlers return `Result<T>`, never throw domain errors).

A logging refactor was partially implemented. A review found the following issues that need to be fixed exactly as described below. Do not change anything else.

---

## Fix 1 — `appsettings.json` (production)

The Console sink `outputTemplate` is missing `{TraceId}`. Replace the current template with:

```
"[{Timestamp:HH:mm:ss} {Level:u3}] [Corr={CorrelationId}] [User={UserId}] [Email={Email}] [Trace={TraceId}] [Env={AppEnvironment}] {Message:lj}{NewLine}{Exception}"
```

---

## Fix 2 — `appsettings.Development.json`

Same problem — `{TraceId}` is missing from the template. Apply the exact same template string as Fix 1.

---

## Fix 3 — `HrSystemApp.Api/Middleware/RequestResponseLoggingMiddleware.cs`

This file has two plan violations and is incomplete. Rewrite it completely:

- **Violation 1:** It reads `context.User` at the TOP of `InvokeAsync`, before `UseAuthentication` has run, so `UserId` and `Email` are always null. User identity MUST be read inside the `finally` block, after `_next(context)` has awaited (at that point auth has already run).
- **Violation 2:** The plan states all `LoggingOptions` flag checks must go through `AppLoggerExtensions`, never directly in middleware. The direct `if (!_loggingOptions.EnableRequestResponseLogging) return;` check at the top is acceptable as a fast-path skip for the whole middleware, but the slow-request logic must not duplicate the toggle check.
- **Violation 3:** It re-pushes `CorrelationId` to `LogContext` — this is already in scope from `CorrelationIdMiddleware`. Do not re-push it.
- Skip paths starting with `/health` or `/swagger`.
- In the `finally` block: read `UserId` from `context.User?.FindFirst("sub")?.Value` (fallback to `ClaimTypes.NameIdentifier`), read `Email` from `context.User?.FindFirst("email")?.Value` (fallback to `ClaimTypes.Email`). Push both plus `StatusCode` and `ElapsedMs` via `LogContext.PushProperty` inside the logging using-block.
- If `ElapsedMs >= SlowOperationThresholdMs` emit `LogWarning`, otherwise `LogInformation`.
- Message template: `"HttpRequest HTTP={HttpMethod} Path={RequestPath} StatusCode={StatusCode} ElapsedMs={ElapsedMs}"` (add `— Slow request` suffix for warnings). Use structured placeholders, never string interpolation.

---

## Fix 4 — `HrSystemApp.Application/Behaviors/LoggingBehavior.cs`

The cheaper model implemented this with a `try/catch` that calls `throw` on the catch block. This violates the Result pattern — handlers return `Result.Failure<T>` for domain errors, never throw. The `throw` causes double-logging (once here, once in `ExceptionMiddleware`) and breaks the pattern.

Rewrite the `Handle` method without any try/catch:

```csharp
public async Task<TResponse> Handle(
    TRequest request,
    RequestHandlerDelegate<TResponse> next,
    CancellationToken cancellationToken)
{
    if (!_loggingOptions.EnableCommandPipelineLogging)
        return await next();

    var actionName = typeof(TRequest).Name;
    var requestId = ExtractRequestId(request);
    var sw = Stopwatch.StartNew();

    _logger.LogActionStart(_loggingOptions, actionName, requestId);

    var response = await next();  // never wrap in try/catch — domain errors come back as Result.Failure

    sw.Stop();

    var isFailure = response is Result r && r.IsFailure;

    if (isFailure)
    {
        var lastKnownState = new { RequestType = actionName };
        _logger.LogActionFailure(_loggingOptions, actionName, LogStage.Processing, lastKnownState, requestId);
    }
    else
    {
        _logger.LogActionSuccess(_loggingOptions, actionName, sw.ElapsedMilliseconds, requestId);
        _logger.LogSlowOperation(_loggingOptions, actionName, sw.ElapsedMilliseconds, requestId);
    }

    return response;
}
```

Add `using HrSystemApp.Application.Common;` so `Result` resolves. Keep `ExtractRequestId` unchanged.

---

## Fix 5 — `HrSystemApp.Application/Behaviors/ValidationBehavior.cs`

On validation failure the plan requires logging **field names only** — never error messages (they may contain user-supplied input). Change the failure log to use the list of invalid property names:

```csharp
var invalidFields = failures.Select(e => e.PropertyName).Distinct().ToList();
_logger.LogDecision(_loggingOptions, actionName, LogStage.Validation,
    "ValidationFailed", new { InvalidFields = invalidFields, RequestId = requestId });
```

---

## Fix 6 — `HrSystemApp.Application/Common/Logging/AppLoggerExtensions.cs`

Add a second overload of `LogActionFailure` that takes **no Exception parameter**. This is needed by `LoggingBehavior` to log Result-pattern failures (which have no exception). Place it immediately after the existing `LogActionFailure(... Exception ex ...)` overload:

```csharp
/// <summary>
/// Logs a domain-level failure captured in a Result — no exception object required.
/// Use when a handler returns Result.Failure (not an infrastructure exception).
/// </summary>
public static void LogActionFailure(
    this ILogger logger, LoggingOptions opts, string action, LogStage stage,
    object lastKnownState, Guid? requestId = null)
{
    if (!IsEnabled(opts, action)) return;
    logger.LogWarning(
        "Action={Action} Stage={Stage} RequestId={RequestId} LastKnownState={@LastKnownState} — Result failure",
        action, stage, requestId, lastKnownState);
}
```

---

## Fix 7 — `HrSystemApp.Api/Program.cs` middleware order

`LoggingScopeMiddleware` must run **after** `UseAuthentication()` and `UseAuthorization()` — not before them — so that `ICurrentUserService.UserId` and `.Email` resolve from the parsed JWT rather than returning null. The correct order is:

```csharp
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<RequestResponseLoggingMiddleware>();
app.UseMiddleware<ExceptionMiddleware>();
app.UseCors("AllowAll");
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<LoggingScopeMiddleware>();  // ← MUST be here, after auth
app.MapControllers();
```

Do not add or remove any other middleware.

---

## Rules

- Do not change any business logic, domain models, repositories, or any file not listed above.
- Compile and confirm there are no build errors before finishing.
