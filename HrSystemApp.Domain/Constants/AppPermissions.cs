namespace HrSystemApp.Domain.Constants;

public static class AppPermissions
{
    public const string AttendanceViewAll = "attendance.view_all";
    public const string AttendanceOverride = "attendance.override";
    public const string RequestViewAll = "requests.view_all";
    public const string RequestManage = "requests.manage";
    public const string RequestCreate = "requests.create";
    public const string EmployeesManage = "employees.manage";
    public const string ReportsView = "reports.view";
    public const string Notifications = "notifications";
    public const string ProfileUpdateRequests = "profile.update_requests";

    public const string CompanyConfigEdit = "company.config.edit";
    public const string LocationsEdit = "locations.edit";
    public const string RolesEdit = "roles.edit";
    public const string HierarchyEdit = "hierarchy.edit";
    public const string WorkflowEdit = "workflow.edit";
    public const string EmployeesEdit = "employees.edit";
    public const string CompaniesManage = "companies.manage";
    public const string ContactAdminRequests = "contact_admin.requests";

    public static readonly IReadOnlyList<string> All = new[]
    {
        AttendanceViewAll,
        AttendanceOverride,
        RequestViewAll,
        RequestManage,
        RequestCreate,
        EmployeesManage,
        ReportsView,
        Notifications,
        ProfileUpdateRequests,
        CompanyConfigEdit,
        LocationsEdit,
        RolesEdit,
        HierarchyEdit,
        WorkflowEdit,
        EmployeesEdit,
        CompaniesManage,
        ContactAdminRequests
    };
}