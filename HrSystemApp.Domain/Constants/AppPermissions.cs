namespace HrSystemApp.Domain.Constants;

/// <summary>
/// All available feature-level permission strings.
/// Each string matches a policy name registered in DI.
/// </summary>
public static class AppPermissions
{
    public const string ViewAllAttendance = "attendance.view_all";
    public const string OverrideAttendance = "attendance.override";
    public const string ViewAllRequests = "requests.view_all";
    public const string ManageEmployees = "employees.manage";
    public const string ViewReports = "reports.view";

    public static readonly IReadOnlyList<string> All = new[]
    {
        ViewAllAttendance,
        OverrideAttendance,
        ViewAllRequests,
        ManageEmployees,
        ViewReports
    };
}
