using Microsoft.Extensions.Logging;

namespace HrSystemApp.Application.Common.Logging;

public static class AppLoggerExtensions
{
    // ── Request lifecycle ──────────────────────────────────────────────────
    // Start     → Information   (request received)
    // Finalization → Information (request completed successfully)
    // ── Business flow → Information (key decision points — "ChainBuilt", "StepAdvanced")
    // ── Validation/Processing details → Debug (field checks, schema internals)
    // ── External calls (DB) → Debug   (GetAncestorsAsync, GetManagersByNodeAsync)
    // ── Failures → Error / Warning

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

    public static void LogActionFailure(
        this ILogger logger, LoggingOptions opts, string action, LogStage stage,
        Exception ex, object lastKnownState, Guid? requestId = null)
    {
        if (!IsEnabled(opts, action)) return;
        logger.LogError(ex,
            "Action={Action} Stage={Stage} RequestId={RequestId} LastKnownState={@LastKnownState}",
            action, stage, requestId, lastKnownState);
    }

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

    /// <summary>
    /// Business-level decision points — e.g. "ChainBuilt", "StepAdvanced", "FullyApproved".
    /// These are key transitions that should appear in Information logs.
    /// </summary>
    public static void LogBusinessFlow(
        this ILogger logger, LoggingOptions opts, string action, LogStage stage,
        string eventKey, object eventData)
    {
        if (!IsEnabled(opts, action)) return;
        logger.LogInformation(
            "Action={Action} Stage={Stage} Event={Event} Data={@Data}",
            action, stage, eventKey, eventData);
    }

    /// <summary>
    /// Internal validation / processing details — field checks, schema internals, detailed paths.
    /// These are Debug-level noise that helps during development but should not appear in Production Information logs.
    /// </summary>
    public static void LogDecision(
        this ILogger logger, LoggingOptions opts, string action, LogStage stage,
        string decisionKey, object decisionValue)
    {
        if (!IsEnabled(opts, action)) return;
        logger.LogDebug(
            "Action={Action} Stage={Stage} Decision={Decision} Value={@Value}",
            action, stage, decisionKey, decisionValue);
    }

    public static void LogWarningUnauthorized(
        this ILogger logger, LoggingOptions opts, string action, Guid? requestId = null)
    {
        if (!IsEnabled(opts, action)) return;
        logger.LogWarning(
            "Action={Action} Stage={Stage} RequestId={RequestId} — Unauthorized attempt",
            action, LogStage.Authorization, requestId);
    }

    /// <summary>
    /// External call timing (database queries, API calls, email sends).
    /// Always Debug — DB queries are Debug-level noise, not business events.
    /// </summary>
    public static void LogExternalCall(
        this ILogger logger, LoggingOptions opts, string action,
        string callTarget, long elapsedMs)
    {
        if (!IsEnabled(opts, action)) return;
        logger.LogDebug(
            "Action={Action} Stage={Stage} ExternalCall={ExternalCall} ElapsedMs={ElapsedMs}",
            action, LogStage.ExternalCall, callTarget, elapsedMs);
    }

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

    private static bool IsEnabled(LoggingOptions opts, string action)
    {
        var category = LogActionCategoryMap.GetCategory(action);

        return category switch
        {
            LogCategory.Workflow   => opts.EnableWorkflowLogging,
            LogCategory.Auth       => opts.EnableAuthLogging,
            LogCategory.OrgNode    => opts.EnableOrgNodeLogging,
            LogCategory.Attendance => opts.EnableAttendanceLogging,
            _                      => true
        };
    }
}
