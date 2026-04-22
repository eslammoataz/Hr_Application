# Structured Logging Refactor Plan
### HrSystemApp — ElasticSearch-Ready, Production-Grade Logging

---

## Goals

- Full end-to-end traceability of every request
- Every log entry carries: **Action**, **Stage**, **UserId**, **Email**, **TraceId**, **RequestId**, **ElapsedMs** (success), **LastKnownState** (error) <!-- UPDATED: ElapsedMs + LastKnownState added as mandatory -->
- Feature-level toggles via `appsettings.json` — **checked only inside `AppLoggerExtensions`, never in handlers** <!-- UPDATED: enforcement point clarified -->
- Zero log noise — only meaningful business events
- Clean, readable handlers — **zero raw logger calls, zero `if (loggingEnabled)` checks inside handlers** <!-- UPDATED: explicit rule -->

---

## Current State

| Area | Status |
|------|--------|
| Serilog installed | ✅ Console + Seq sinks |
| CorrelationIdMiddleware | ✅ Exists — pushes CorrelationId |
| RequestResponseLoggingMiddleware | ✅ Exists — times requests |
| ExceptionMiddleware | ✅ Exists — catches unhandled exceptions |
| Centralized logging helper | ❌ None |
| Consistent log structure | ❌ Ad-hoc per handler |
| Feature-level toggles | ❌ None |
| ElasticSearch sink | ❌ Not configured |
| Email in log context | ❌ Not in ICurrentUserService |
| MediatR logging behavior | ❌ None |
| Strong-typed Stage | ❌ No enum — strings used ad-hoc <!-- UPDATED: gap identified --> |
| Masking / safety controls | ❌ None <!-- NEW: gap identified --> |

---

## Global Log Schema

<!-- UPDATED: ElapsedMs and LastKnownState added as mandatory fields -->

Every log entry — regardless of source — must carry these structured properties:

```
Action          = "ApproveRequest"          (what operation — from LogAction constants)
Stage           = LogStage.Processing       (strongly-typed enum — NEVER a raw string)
RequestId       = "3fa85f64-..."            (domain request ID, when applicable — null otherwise)
UserId          = "auth0|abc123"            (from JWT — injected by LoggingScopeMiddleware)
Email           = "user@company.com"        (from JWT — injected by LoggingScopeMiddleware)
TraceId         = "0af7651916cd43dd..."     (ASP.NET Activity.TraceId)
AppEnvironment  = "Production"              (injected by LoggingScopeMiddleware — NEW)
ElapsedMs       = 142                       (mandatory on ALL success logs — NEW)
LastKnownState  = { StepOrder: 2, ... }     (mandatory on ALL error logs — structured object, NOT string — NEW)
```

**Enforcement:** All of the above except `RequestId` and `LastKnownState` are pushed to the Serilog scope by `LoggingScopeMiddleware`. Handlers never set them manually. `RequestId` is passed as a parameter to extension methods. `LastKnownState` is passed only on error methods.

---

## Log Level Rules

| Level | When to use |
|-------|-------------|
| `Debug` | Inputs/outputs to operations, decision outcomes — **Dev only, disabled in Production via appsettings** |
| `Information` | Stage transitions, successful operations, business flow |
| `Warning` | Invalid/unauthorized attempts, unexpected-but-handled scenarios |
| `Error` | Exceptions — always includes Stage, Action, LastKnownState |

<!-- NEW: Explicit "never log" rules -->
**Never log:**
- Passwords, tokens, OTPs, refresh tokens, API keys — in any form
- Full request/response bodies (use field-level masking — see Phase 1)
- Variable assignments, loop internals, trivial null checks
- The same stage twice in one handler
- Entire entity objects — log only the IDs and decision-relevant fields
- Validation error *messages* — log only field *names* (messages may contain user-supplied input)
- Any string built with `$"..."` interpolation as a log message — always use structured placeholders

---

## Log Safety Rules <!-- NEW SECTION -->

These rules are non-negotiable and must be enforced during code review.

| Rule | Detail |
|------|--------|
| No credentials | Never log `Password`, `Token`, `RefreshToken`, `OtpCode`, `ApiKey` |
| No full payloads | Request/response bodies are masked — only safe fields are logged |
| Field masking | Fields named `password`, `token`, `secret`, `otp`, `card` are replaced with `[REDACTED]` |
| Payload size cap | Request body logged only if `ContentLength < 4096 bytes`; truncate otherwise |
| No PII beyond scope | Only `UserId` and `Email` are acceptable identity fields in logs |
| Structured only | All log messages use `{Placeholder}` syntax — zero string interpolation for data |
| LastKnownState is an object | Pass as anonymous type: `new { StepOrder = 2, Status = "InProgress" }` — never a string |

---

## appsettings Structure

### appsettings.json (Production — minimal noise)

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning",
        "Microsoft.EntityFrameworkCore": "Warning",
        "HrSystemApp": "Information"
      }
    }
  },
  "LoggingOptions": {
    "EnableWorkflowLogging": true,
    "EnableAuthLogging": true,
    "EnableOrgNodeLogging": true,
    "EnableAttendanceLogging": false,
    "EnableCommandPipelineLogging": true,
    "EnableRequestResponseLogging": false,
    "RequestBodyMaxLogBytes": 4096,
    "SlowOperationThresholdMs": 2000
  },
  "ElasticSettings": {
    "Enabled": false,
    "NodeUri": "http://localhost:9200",
    "IndexPrefix": "hrsystemapp",
    "Environment": "production"
  }
}
```

<!-- UPDATED: ElasticSettings now uses IndexPrefix + Environment for environment-aware index naming -->
<!-- NEW: RequestBodyMaxLogBytes and SlowOperationThresholdMs added -->

### appsettings.Development.json (Verbose)

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Debug",
      "Override": {
        "Microsoft": "Information",
        "Microsoft.EntityFrameworkCore.Database.Command": "Information",
        "HrSystemApp": "Debug"
      }
    }
  },
  "LoggingOptions": {
    "EnableWorkflowLogging": true,
    "EnableAuthLogging": true,
    "EnableOrgNodeLogging": true,
    "EnableAttendanceLogging": true,
    "EnableCommandPipelineLogging": true,
    "EnableRequestResponseLogging": true,
    "RequestBodyMaxLogBytes": 4096,
    "SlowOperationThresholdMs": 500
  },
  "ElasticSettings": {
    "Enabled": false,
    "NodeUri": "http://localhost:9200",
    "IndexPrefix": "hrsystemapp",
    "Environment": "development"
  }
}
```

<!-- NEW: appsettings.Staging.json should set Environment: "staging" and a tighter MinimumLevel -->

---

## Execution Phases

---

### Phase 0 — Foundation Infrastructure
> **Risk: Zero** — new files only, no business logic touched

**New folder:** `HrSystemApp.Application/Common/Logging/`

#### Files to create

**`LogAction.cs`**
<!-- UPDATED: Constants organised into nested static classes named after their LogCategory.
     The nested class name is the source of truth for the category mapping — no dictionary to maintain manually.
     Callers use LogAction.Workflow.ApproveRequest etc. Adding a new action = add it to the right nested class. -->
```csharp
public static class LogAction
{
    public static class Workflow
    {
        public const string ApproveRequest           = "ApproveRequest";
        public const string CreateRequest            = "CreateRequest";
        public const string RejectRequest            = "RejectRequest";
        public const string UpdateRequest            = "UpdateRequest";
        public const string DeleteRequest            = "DeleteRequest";
        public const string CreateRequestDefinition  = "CreateRequestDefinition";
        public const string UpdateRequestDefinition  = "UpdateRequestDefinition";
        public const string DeleteRequestDefinition  = "DeleteRequestDefinition";
    }

    public static class Auth
    {
        public const string LoginUser         = "LoginUser";
        public const string RegisterUser      = "RegisterUser";
        public const string ChangePassword    = "ChangePassword";
        public const string ForceChangePassword = "ForceChangePassword";
        public const string ForgotPassword    = "ForgotPassword";
        public const string ResetPassword     = "ResetPassword";
        public const string VerifyOtp         = "VerifyOtp";
        public const string RefreshToken      = "RefreshToken";
        public const string LogoutUser        = "LogoutUser";
        public const string RevokeToken       = "RevokeToken";
        public const string RevokeAllTokens   = "RevokeAllTokens";
    }

    public static class OrgNode
    {
        public const string CreateOrgNode             = "CreateOrgNode";
        public const string UpdateOrgNode             = "UpdateOrgNode";
        public const string DeleteOrgNode             = "DeleteOrgNode";
        public const string AssignEmployeeToNode      = "AssignEmployeeToNode";
        public const string UnassignEmployeeFromNode  = "UnassignEmployeeFromNode";
        public const string BulkSetupOrgNodes         = "BulkSetupOrgNodes";
        public const string CreateEmployee            = "CreateEmployee";
        public const string UpdateEmployee            = "UpdateEmployee";
        public const string ChangeEmployeeStatus      = "ChangeEmployeeStatus";
    }

    public static class Attendance
    {
        public const string ClockIn              = "ClockIn";
        public const string ClockOut             = "ClockOut";
        public const string OverrideClockIn      = "OverrideClockIn";
        public const string OverrideClockOut     = "OverrideClockOut";
        public const string AttendanceReminder   = "AttendanceReminder";
        public const string AutoClockOut         = "AutoClockOut";
    }
    // Adding a new action to an existing category: add the constant to the matching nested class.
    // Adding a new category: add a nested class + add the LogCategory enum value + extend IsEnabled switch.
}
```

<!-- UPDATED: LogStage is now an enum, not a string constants class -->
**`LogStage.cs`**
Strongly-typed enum — enforces consistent stage values across the entire codebase.
Raw strings are forbidden. Serilog serializes enums as their name string, which is ElasticSearch-queryable.
```csharp
public enum LogStage
{
    Start,
    Validation,
    Authorization,
    Processing,
    ExternalCall,
    Finalization
}
```

<!-- NEW: LogCategory enum drives the IsEnabled category switch -->
**`LogCategory.cs`**
Enum that maps action groups to their `LoggingOptions` flag.
One value per feature toggle. Extend only when a new toggle is added to `LoggingOptions`.
```csharp
public enum LogCategory
{
    Workflow,
    Auth,
    OrgNode,
    Attendance,
    Default   // safe fallback — always logs, unmapped actions land here
}
```

<!-- NEW: LogActionCategoryMap — single source of truth for action→category resolution -->
**`LogActionCategoryMap.cs`**
Builds the `action → category` dictionary **once at startup** via reflection on `LogAction`'s nested
classes. Each nested class name must exactly match a `LogCategory` enum value.

**Extending the map:** add the constant to the right nested class in `LogAction` — nothing else changes.
**Adding a new category:** add nested class in `LogAction` + `LogCategory` value + one arm in `IsEnabled`.

```csharp
internal static class LogActionCategoryMap
{
    // Populated once at startup — zero per-request overhead.
    private static readonly Dictionary<string, LogCategory> _map =
        BuildFromNestedClasses();

    /// <summary>
    /// Returns the category for the given action name.
    /// Falls back to <see cref="LogCategory.Default"/> for unmapped actions — never throws.
    /// </summary>
    public static LogCategory GetCategory(string action) =>
        _map.GetValueOrDefault(action, LogCategory.Default);

    // Reflects over LogAction's public nested classes.
    // Each nested class whose name parses to a LogCategory is iterated;
    // every public string const inside is registered under that category.
    private static Dictionary<string, LogCategory> BuildFromNestedClasses()
    {
        var map = new Dictionary<string, LogCategory>(StringComparer.Ordinal);

        foreach (var nested in typeof(LogAction).GetNestedTypes(BindingFlags.Public | BindingFlags.Static))
        {
            if (!Enum.TryParse<LogCategory>(nested.Name, ignoreCase: true, out var category))
                continue; // nested class not matching a LogCategory — skip safely

            foreach (var field in nested.GetFields(
                         BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy))
            {
                if (field.IsLiteral && !field.IsInitOnly && field.GetValue(null) is string actionName)
                    map[actionName] = category;
            }
        }

        return map;
    }
}
```

<!-- UPDATED: AppLoggerExtensions now owns ALL LoggingOptions checks and enforces full schema -->
**`AppLoggerExtensions.cs`**
The **only** place where logging options are checked. Handlers call these methods unconditionally —
they never check flags themselves. Every method enforces the full log schema.

```csharp
// ── Success log — ElapsedMs is MANDATORY ──────────────────────────────────
public static void LogActionStart(
    this ILogger logger, LoggingOptions opts, string action, Guid? requestId = null)
{
    if (!IsEnabled(opts, action)) return;
    logger.LogInformation(
        "Action={Action} Stage={Stage} RequestId={RequestId}",
        action, LogStage.Start, requestId);
}

public static void LogActionSuccess(
    this ILogger logger, LoggingOptions opts, string action, long elapsedMs, Guid? requestId = null)
{
    if (!IsEnabled(opts, action)) return;
    logger.LogInformation(
        "Action={Action} Stage={Stage} RequestId={RequestId} ElapsedMs={ElapsedMs}",
        action, LogStage.Finalization, requestId, elapsedMs);
}

// ── Error log — LastKnownState is MANDATORY, typed as object ──────────────
public static void LogActionFailure(
    this ILogger logger, LoggingOptions opts, string action, LogStage stage,
    Exception ex, object lastKnownState, Guid? requestId = null)
{
    if (!IsEnabled(opts, action)) return;
    logger.LogError(ex,
        "Action={Action} Stage={Stage} RequestId={RequestId} LastKnownState={@LastKnownState}",
        action, stage, requestId, lastKnownState);
}

// ── Decision / key transition — Debug level ───────────────────────────────
public static void LogDecision(
    this ILogger logger, LoggingOptions opts, string action, LogStage stage,
    string decisionKey, object decisionValue)
{
    if (!IsEnabled(opts, action)) return;
    logger.LogDebug(
        "Action={Action} Stage={Stage} Decision={Decision} Value={@Value}",
        action, stage, decisionKey, decisionValue);
}

// ── Unauthorized attempt — Warning ────────────────────────────────────────
public static void LogWarningUnauthorized(
    this ILogger logger, LoggingOptions opts, string action, Guid? requestId = null)
{
    if (!IsEnabled(opts, action)) return;
    logger.LogWarning(
        "Action={Action} Stage={Stage} RequestId={RequestId} — Unauthorized attempt",
        action, LogStage.Authorization, requestId);
}

// ── External call (DB, API, email) ────────────────────────────────────────
public static void LogExternalCall(
    this ILogger logger, LoggingOptions opts, string action,
    string callTarget, long elapsedMs)
{
    if (!IsEnabled(opts, action)) return;
    logger.LogDebug(
        "Action={Action} Stage={Stage} ExternalCall={ExternalCall} ElapsedMs={ElapsedMs}",
        action, LogStage.ExternalCall, callTarget, elapsedMs);
}

// ── Slow operation warning ────────────────────────────────────────────────
// NEW: emits a Warning if elapsedMs exceeds configured threshold
public static void LogSlowOperation(
    this ILogger logger, LoggingOptions opts, string action,
    long elapsedMs, Guid? requestId = null)
{
    if (!IsEnabled(opts, action)) return;
    if (elapsedMs < opts.SlowOperationThresholdMs) return;
    logger.LogWarning(
        "Action={Action} Stage={Stage} RequestId={RequestId} ElapsedMs={ElapsedMs} — Slow operation detected",
        action, LogStage.Finalization, requestId, elapsedMs);
}

// ── Internal helper — feature toggle gate ────────────────────────────────
// UPDATED: replaces the unmaintainable action-level switch with a two-step lookup.
//   Step 1: resolve action → LogCategory  (via LogActionCategoryMap — dictionary, O(1))
//   Step 2: resolve LogCategory → bool    (via category switch — never needs changing for new actions)
private static bool IsEnabled(LoggingOptions opts, string action)
{
    var category = LogActionCategoryMap.GetCategory(action); // falls back to Default if unmapped

    return category switch
    {
        LogCategory.Workflow   => opts.EnableWorkflowLogging,
        LogCategory.Auth       => opts.EnableAuthLogging,
        LogCategory.OrgNode    => opts.EnableOrgNodeLogging,
        LogCategory.Attendance => opts.EnableAttendanceLogging,
        _                      => true   // Default: always log
    };
}
```

<!-- UPDATED: LoggingOptions adds two new fields -->
**`LoggingOptions.cs`**
POCO bound from appsettings — injected via `IOptions<LoggingOptions>`.
All feature-flag checks are centralized in `AppLoggerExtensions.IsEnabled()` — never evaluated in handlers.
```csharp
public class LoggingOptions
{
    public bool EnableWorkflowLogging        { get; set; } = true;
    public bool EnableAuthLogging            { get; set; } = true;
    public bool EnableOrgNodeLogging         { get; set; } = true;
    public bool EnableAttendanceLogging      { get; set; } = false;
    public bool EnableCommandPipelineLogging { get; set; } = true;
    public bool EnableRequestResponseLogging { get; set; } = false;
    public int  RequestBodyMaxLogBytes       { get; set; } = 4096;   // NEW
    public int  SlowOperationThresholdMs     { get; set; } = 2000;   // NEW
}
```

#### Files to modify

**`ICurrentUserService.cs`** — add `string? Email` property.
Needed so `LoggingScopeMiddleware` can push Email into the log scope automatically.

---

### Phase 1 — Middleware & MediatR Pipeline
> **Risk: Low** — cross-cutting only, zero business logic changes

After this phase, every command and query gets automatic Start/Success/Failure logs via the pipeline — handlers don't need to add this themselves.

#### New file: `LoggingScopeMiddleware.cs`

Runs **first** in the pipeline. Pushes a Serilog scope for the entire request lifetime.
Every downstream log automatically includes these fields — no handler sets them manually.

```
Fields pushed to scope:
  UserId         → from JWT claims
  Email          → from JWT claims
  TraceId        → from Activity.Current?.TraceId ?? HttpContext.TraceIdentifier  // UPDATED: prefer W3C TraceId
  RequestPath    → from HttpContext.Request.Path
  HttpMethod     → from HttpContext.Request.Method
  AppEnvironment → from IWebHostEnvironment.EnvironmentName                       // NEW
```

Replaces the manual `LogContext.PushProperty` calls scattered in existing middleware.

<!-- UPDATED: ExceptionMiddleware now uses LastKnownState as structured object -->
#### Modify: `ExceptionMiddleware.cs`

Replace ad-hoc log message with `AppLoggerExtensions.LogActionFailure`.
`LastKnownState` must be a structured object: `new { Path = context.Request.Path, Method = context.Request.Method }`.
No string interpolation.

<!-- UPDATED: Adds masking + payload size cap -->
#### Modify: `RequestResponseLoggingMiddleware.cs`

- Guard entire body behind `AppLoggerExtensions` — middleware does **not** check `_options.EnableRequestResponseLogging` directly; it calls a single extension method that handles the guard
- Skip health check endpoint (`/health`) and any path prefixed `/swagger`
- **Masking:** Strip headers `Authorization`, `Cookie`, `X-Api-Key` before logging
- **Body size cap:** Only log request body if `ContentLength < _options.RequestBodyMaxLogBytes`; otherwise log `"[BODY TRUNCATED: {ContentLength} bytes]"` — never truncate silently
- **Slow response warning:** If elapsed > `_options.SlowOperationThresholdMs`, emit Warning instead of Information
- Standardize field names: `HttpMethod`, `RequestPath`, `StatusCode`, `ElapsedMs`

<!-- UPDATED: LoggingBehavior takes LoggingOptions, calls AppLoggerExtensions exclusively -->
#### New file: `LoggingBehavior.cs` (MediatR `IPipelineBehavior<TRequest, TResponse>`)

The single most impactful change. Runs automatically for **every** command and query.
Handlers that use this behavior contain **zero** start/success/failure log calls.

```
On entry:   logger.LogActionStart(opts, actionName, requestId)
On success: logger.LogActionSuccess(opts, actionName, elapsedMs, requestId)
            logger.LogSlowOperation(opts, actionName, elapsedMs, requestId)   // NEW
On failure: logger.LogActionFailure(opts, actionName, LogStage.Processing,
                                    ex, lastKnownState, requestId)
```

- `actionName` derived from `typeof(TRequest).Name` — no manual string needed
- `requestId` extracted via interface: commands/queries that carry a domain RequestId implement `IHaveRequestId` <!-- NEW: interface for clean extraction -->
- Registered in DI **after** `ValidationBehavior` so validation failures are also captured

<!-- UPDATED: ValidationBehavior logs field names only — never values, never messages -->
#### Modify: `ValidationBehavior.cs`

- On entry: `logger.LogDecision(opts, actionName, LogStage.Validation, "ValidationStarted", commandType)`
- On failure: `LogWarning` with **field names only** — `new { Fields = errors.Select(e => e.PropertyName) }` — never log the error message text (may contain user input)
- On pass: `LogDebug` stage passed — single line, no details
- **No** `if (_options.EnableX)` check — the extension method handles it

---

### Phase 2 — Request / Approval Workflow
> **Risk: Medium** — core business flow, adds try/catch wrappers
> **Guard:** Handled entirely by `AppLoggerExtensions.IsEnabled()` — handlers contain no guard checks <!-- UPDATED -->

This is the highest-value flow. Full stage traceability from submission to final approval.

<!-- UPDATED: All tables now include ElapsedMs on success rows and LastKnownState on error rows -->

#### `CreateRequestCommand.cs` — 5 log points

| Stage | Level | What is logged | ElapsedMs | LastKnownState |
|-------|-------|---------------|-----------|---------------|
| Start | Info | EmployeeId, RequestType | — | — |
| Authorization | Info | Employee found — EmployeeId | — | — |
| Processing | Info | Chain resolved — StepCount, StepTypes (names only) | — | — |
| Finalization | Info | Request saved — new RequestId | ✅ total | — |
| Error | Error | try/catch wraps full handler body | — | `{ EmployeeId, RequestType, Stage }` |

#### `ApproveRequestCommand.cs` — 6 log points

| Stage | Level | What is logged | ElapsedMs | LastKnownState |
|-------|-------|---------------|-----------|---------------|
| Start | Info | ApproverId, RequestId | — | — |
| Authorization | Info | Approver confirmed for current step | — | — |
| Authorization | Warn | Unauthorized attempt — ApproverId, RequestId | — | — |
| Processing | Info | Decision key: `StepAdvanced` or `FullyApproved`, new CurrentStepOrder | — | — |
| Finalization | Info | Status persisted | ✅ total | — |
| Error | Error | Exception caught | — | `{ RequestId, CurrentStepOrder, ApproverId }` |

#### `RejectRequestCommand.cs` — same structure as Approve

#### `WorkflowResolutionService.cs` — 3 log points

| Stage | Level | What is logged | ElapsedMs | LastKnownState |
|-------|-------|---------------|-----------|---------------|
| ExternalCall | Debug | `GetAncestorsAsync` — NodeId, AncestorCount | ✅ per call | — |
| Processing | Debug | StepType resolved, ApproverCount | — | — |
| Warning | Warn | Empty chain resolved — auto-approve will trigger | — | — |

**No per-ancestor-level loop logging** — only aggregate counts logged. <!-- UPDATED: explicit no-loop rule -->

#### Query handlers (`GetPendingApprovals`, `GetRequestById`, `GetMyApprovalActions`)

`LoggingBehavior` covers Start/Success/Failure automatically.
Only add: `LogDebug` — `ResultCount` at Finalization. Nothing else.

---

### Phase 3 — Auth Flow
> **Risk: Low** — warnings dominate, no state mutations to wrap
> **Guard:** Handled entirely by `AppLoggerExtensions.IsEnabled()` <!-- UPDATED -->

Security-sensitive. Warnings matter more than Info here.

#### Handlers covered

`LoginUser`, `RegisterUser`, `ChangePassword`, `ForgotPassword`, `ResetPassword`, `VerifyOtp`, `RefreshToken`, `LogoutUser`

<!-- UPDATED: Stronger safety rules, LastKnownState on errors -->

#### Log points per handler

| Stage | Level | What is logged | LastKnownState |
|-------|-------|---------------|---------------|
| Start | Info | Action name, UserId (if available) — **no credentials** | — |
| Authorization | Warn | Failed auth — ReasonCode only (e.g. `"InvalidCredentials"`) | — |
| Authorization | Warn | Expired/invalid OTP — no OTP value logged | — |
| Authorization | Warn | Account locked — UserId | — |
| Finalization | Info | Success — UserId, ElapsedMs | — |
| Error | Error | External call failure (email/SMS) | `{ Action, Stage }` |

`LoggingBehavior` handles Start/Success/Failure wrapping automatically.
Handlers only add the Warning rows — everything else comes from the pipeline.

**Absolute rule for this phase:** Fields named `Password`, `Token`, `OtpCode`, `RefreshToken` must never appear in any log statement, even as `null`.

---

### Phase 4 — OrgNode & Employee Flows
> **Risk: Low**
> **Guard:** Handled entirely by `AppLoggerExtensions.IsEnabled()` <!-- UPDATED -->

Structural operations — important for audit trail.

#### OrgNode commands

`CreateOrgNode`, `UpdateOrgNode`, `DeleteOrgNode`, `AssignEmployeeToNode`, `UnassignEmployeeFromNode`

| Stage | Level | What is logged |
|-------|-------|---------------|
| Processing | Info | NodeId, ParentId |
| Processing | Info | EmployeeId, NodeId, OrgRole |
| Warning | Warn | `AssignmentAlreadyExists` — NodeId, EmployeeId |
| Warning | Warn | `DeleteNodeWithChildren` — NodeId, ChildCount |

#### Employee commands

`CreateEmployee`, `UpdateEmployee`, `ChangeEmployeeStatus`

| Stage | Level | What is logged |
|-------|-------|---------------|
| Finalization | Info | EmployeeId, CompanyId, ElapsedMs |
| Warning | Warn | Status changed to Inactive — EmployeeId |

---

### Phase 5 — Infrastructure Services
> **Risk: Low** — these are called from handlers, `LoggingBehavior` won't reach them
> **Guard:** Service-specific toggle checked inside `AppLoggerExtensions`, not in service code <!-- UPDATED -->

<!-- UPDATED: ElapsedMs added to all ExternalCall rows, LastKnownState added to error rows -->

| Service | Stage | Level | What is logged | ElapsedMs |
|---------|-------|-------|---------------|-----------|
| `EmailService` | ExternalCall | Debug | Recipient (masked to domain only: `***@company.com`), TemplateId | ✅ |
| `EmailService` | — | Warn | Send failure — TemplateId, RecipientDomain (no address) | — |
| `NotificationService` | Processing | Info | NotificationType, RecipientCount | — |
| `AttendanceReminderService` | Start/Finalization | Info | ScheduledRunId, EmployeeCount | ✅ |
| `AutoClockOutService` | Start/Finalization | Info | ScheduledRunId, ClockOutCount | ✅ |

Guard attendance services behind `LoggingOptions.EnableAttendanceLogging`.

**Email masking rule:** Never log a full email address in `EmailService` — log only the domain part. <!-- NEW -->

---

### Phase 6 — ElasticSearch Sink & Final Configuration
> **Risk: Zero** — configuration only

#### Add NuGet package

```
Elastic.Serilog.Sinks
```
(Official Elastic package — newer than the community `Serilog.Sinks.Elasticsearch`)

<!-- UPDATED: Environment-aware index naming: hrsystemapp-production-2025.01.15 -->
#### Modify `Program.cs`

```csharp
// Same conditional pattern as existing Seq sink
var elasticEnabled = builder.Configuration.GetValue<bool>("ElasticSettings:Enabled");
if (elasticEnabled)
{
    var nodeUri    = builder.Configuration["ElasticSettings:NodeUri"];
    var prefix     = builder.Configuration["ElasticSettings:IndexPrefix"];   // e.g. "hrsystemapp"
    var env        = builder.Configuration["ElasticSettings:Environment"];   // e.g. "production"
    // Index format: hrsystemapp-production-2025.01.15
    var indexFormat = $"{prefix}-{env}-{{0:yyyy.MM.dd}}";

    loggerConfig.WriteTo.Elasticsearch(new[] { new Uri(nodeUri) }, opts =>
    {
        opts.DataStream          = new DataStreamName("logs", prefix, env);
        opts.BootstrapMethod     = BootstrapMethod.Failure;
        opts.TextFormatting      = new EcsTextFormatterConfiguration(); // ECS-compliant format
    });
}
```

<!-- NEW: ElasticSearch queryable fields -->
**Key queryable fields in ElasticSearch after this setup:**
```
fields.Action          → filter by operation type
fields.Stage           → filter by lifecycle stage
fields.RequestId       → trace a single request end-to-end
fields.UserId          → all actions by one user
fields.ElapsedMs       → identify slow operations (range query)
fields.AppEnvironment  → separate dev/staging/prod indices
log.level              → filter errors/warnings only
```

#### Register `LoggingOptions` in DI

```csharp
// In Program.cs or DependencyInjection.cs
services.Configure<LoggingOptions>(configuration.GetSection("LoggingOptions"));
```

---

## Phase Execution Summary

| Phase | Scope | Touches Business Logic | Risk |
|-------|-------|----------------------|------|
| **0** | New logging foundation files + appsettings | No | Zero |
| **1** | Middleware + MediatR LoggingBehavior | No (cross-cutting only) | Low |
| **2** | Approval/Request workflow commands + WorkflowResolutionService | Minimal (try/catch) | Medium |
| **3** | Auth command handlers | No (warnings only) | Low |
| **4** | OrgNode + Employee commands | No | Low |
| **5** | Infrastructure services | No | Low |
| **6** | Program.cs + appsettings (ES sink) | No | Zero |

---

## What Each Feature Toggle Controls

| Toggle | Controls | Checked by |
|--------|----------|-----------|
| `EnableCommandPipelineLogging` | `LoggingBehavior` — Start/Success/Failure on every command/query | `AppLoggerExtensions` |
| `EnableWorkflowLogging` | All request/approval/workflow handler logs (Phase 2) | `AppLoggerExtensions` |
| `EnableAuthLogging` | All auth handler logs (Phase 3) | `AppLoggerExtensions` |
| `EnableOrgNodeLogging` | OrgNode + Employee handler logs (Phase 4) | `AppLoggerExtensions` |
| `EnableAttendanceLogging` | Attendance reminder + auto clock-out services (Phase 5) | `AppLoggerExtensions` |
| `EnableRequestResponseLogging` | HTTP request/response middleware (Dev-only by default) | `AppLoggerExtensions` |

<!-- UPDATED: Rule explicitly stated -->
**Rule:** The `Checked by` column must always be `AppLoggerExtensions`. If any handler, middleware, or service directly reads a `LoggingOptions` flag, it is a violation of this plan.

Serilog minimum-level overrides in `appsettings` control **verbosity** (Debug vs Info vs Warning).
`LoggingOptions` flags control **whether a domain area logs at all**.

---

## Files Created/Modified — Complete List

### New files

```
HrSystemApp.Application/Common/Logging/LogAction.cs            ← nested by category (Workflow / Auth / OrgNode / Attendance)
HrSystemApp.Application/Common/Logging/LogCategory.cs          ← enum: Workflow, Auth, OrgNode, Attendance, Default
HrSystemApp.Application/Common/Logging/LogActionCategoryMap.cs ← reflection-built dictionary, O(1) lookup, zero per-request cost
HrSystemApp.Application/Common/Logging/LogStage.cs             ← enum, not string constants
HrSystemApp.Application/Common/Logging/AppLoggerExtensions.cs  ← owns ALL toggle checks + full schema
HrSystemApp.Application/Common/Logging/LoggingOptions.cs
HrSystemApp.Application/Interfaces/IHaveRequestId.cs           ← interface for RequestId extraction in LoggingBehavior
HrSystemApp.Api/Middleware/LoggingScopeMiddleware.cs
HrSystemApp.Application/Behaviors/LoggingBehavior.cs
```

### Modified files

```
HrSystemApp.Application/Interfaces/Services/ICurrentUserService.cs   ← add Email property
HrSystemApp.Infrastructure/Services/CurrentUserService.cs            ← implement Email
HrSystemApp.Api/Middleware/ExceptionMiddleware.cs                    ← LogActionFailure + structured LastKnownState
HrSystemApp.Api/Middleware/RequestResponseLoggingMiddleware.cs        ← masking + size cap + slow warning
HrSystemApp.Application/Behaviors/ValidationBehavior.cs              ← field names only, no values
HrSystemApp.Api/Program.cs                                           ← ES sink, IHaveRequestId registration
HrSystemApp.Api/appsettings.json
HrSystemApp.Api/appsettings.Development.json

-- Phase 2 --
HrSystemApp.Application/Features/Requests/Commands/CreateRequest/CreateRequestCommand.cs
HrSystemApp.Application/Features/Requests/Commands/ApproveRequest/ApproveRequestCommand.cs
HrSystemApp.Application/Features/Requests/Commands/RejectRequest/RejectRequestCommand.cs
HrSystemApp.Infrastructure/Services/WorkflowResolutionService.cs

-- Phase 3 --
HrSystemApp.Application/Features/Auth/Commands/LoginUser/LoginUserCommandHandler.cs
HrSystemApp.Application/Features/Auth/Commands/RegisterUser/RegisterUserCommandHandler.cs
(+ 6 more auth handlers)

-- Phase 4 --
(5 OrgNode command handlers + 3 Employee command handlers)

-- Phase 5 --
HrSystemApp.Infrastructure/Services/EmailService.cs
HrSystemApp.Infrastructure/Services/NotificationService.cs
HrSystemApp.Infrastructure/Services/AttendanceReminderService.cs
HrSystemApp.Infrastructure/Services/AutoClockOutService.cs
```

---

## Key Architectural Rules — Summary <!-- NEW SECTION -->

These must be enforced in every PR touching logging:

| # | Rule |
|---|------|
| 1 | `LogStage` is always an enum value — never a raw string |
| 2 | All `LoggingOptions` flag checks live only in `AppLoggerExtensions.IsEnabled()` |
| 3 | Handlers contain zero raw `_logger.LogX(...)` calls — only `AppLoggerExtensions` methods |
| 4 | `ElapsedMs` is required on every success log |
| 5 | `LastKnownState` is required on every error log — always a structured `object`, never a string |
| 6 | Passwords, tokens, OTPs never appear in any log statement |
| 7 | Validation logs contain field *names* only — never field *values* or error *messages* |
| 8 | Request bodies are logged only under `RequestBodyMaxLogBytes`; masked otherwise |
| 9 | ES index name always includes environment: `{prefix}-{env}-{date}` |
| 10 | `LoggingBehavior` handles Start/Success/Failure — handlers only log decisions and warnings |

---

*Approve a phase to begin execution. Each phase will be completed and confirmed before the next starts.*
