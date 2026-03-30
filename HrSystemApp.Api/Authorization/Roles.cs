using HrSystemApp.Domain.Enums;

namespace HrSystemApp.Api.Authorization;

/// <summary>
/// String constants for role-based authorization attributes.
/// Values are derived from <see cref="UserRole"/> enum names — single source of truth.
/// Usage: [Authorize(Roles = Roles.HR)]
///        [Authorize(Roles = Roles.HrOrAbove)]
/// </summary>
public static class Roles
{
    // ── Individual roles ────────────────────────────────────────────────
    public const string SuperAdmin = nameof(UserRole.SuperAdmin);
    public const string CEO = nameof(UserRole.CEO);
    public const string VicePresident = nameof(UserRole.VicePresident);
    public const string DepartmentManager = nameof(UserRole.DepartmentManager);
    public const string UnitLeader = nameof(UserRole.UnitLeader);
    public const string TeamLeader = nameof(UserRole.TeamLeader);
    public const string HR = nameof(UserRole.HR);
    public const string AssetAdmin = nameof(UserRole.AssetAdmin);
    public const string Employee = nameof(UserRole.Employee);
    public const string CompanyAdmin = nameof(UserRole.CompanyAdmin);

    // ── Composite role sets (comma-separated — ASP.NET Core OR logic) ──
    public const string SuperAdminOnly =
        SuperAdmin;

    public const string CeoOrAbove =
        $"{SuperAdmin},{CEO}";

    public const string VpOrAbove =
        $"{SuperAdmin},{CEO},{VicePresident}";

    public const string ManagerOrAbove =
        $"{SuperAdmin},{CEO},{VicePresident},{DepartmentManager}";

    public const string HrOrAbove =
        $"{SuperAdmin},{CEO},{VicePresident},{DepartmentManager},{UnitLeader},{TeamLeader},{HR}";

    public const string HierarchyManagers =
        $"{SuperAdmin},{CEO},{DepartmentManager},{HR}";

    public const string UnitManagers =
        $"{SuperAdmin},{CEO},{VicePresident},{DepartmentManager},{UnitLeader},{HR}";

    public const string TeamManagers =
        $"{SuperAdmin},{CEO},{VicePresident},{DepartmentManager},{UnitLeader},{HR}";

    public const string AssetAdmins =
        $"{SuperAdmin},{AssetAdmin}";

    public const string Viewers =
        $"{SuperAdmin},{CEO},{VicePresident},{DepartmentManager},{UnitLeader},{TeamLeader},{HR}";

    public const string CompanyAdmins =
        $"{SuperAdmin},{CompanyAdmin}";
}
