namespace HrSystemApp.Application.Common;

/// <summary>
/// Application-wide configuration constants
/// </summary>
public static class ConfigurationConstants
{
    /// <summary>
    /// Maximum allowed page size for pagination
    /// </summary>
    public const int MaxPageSize = 100;

    /// <summary>
    /// Default page size for pagination
    /// </summary>
    public const int DefaultPageSize = 10;

    /// <summary>
    /// JWT token lifespan in minutes
    /// </summary>
    public const int TokenLifespanMinutes = 15;

    /// <summary>
    /// Clock skew tolerance for token validation in minutes
    /// </summary>
    public const int ClockSkewMinutes = 0;

    /// <summary>
    /// Prefix for employee code generation
    /// </summary>
    public const string EmployeeCodePrefix = "EMP";

    /// <summary>
    /// Correlation ID header name
    /// </summary>
    public const string CorrelationIdHeader = "X-Correlation-ID";
}
