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
    public const string Executive = nameof(UserRole.Executive);
    public const string HR = nameof(UserRole.HR);
    public const string Employee = nameof(UserRole.Employee);

    // ── Composite role sets (comma-separated — ASP.NET Core OR logic) ──
    public const string SuperAdminOnly = SuperAdmin;

    public const string ExecutiveOrAbove =
        $"{SuperAdmin},{Executive}";

    public const string HrOrAbove =
        $"{SuperAdmin},{Executive},{HR}";

    public const string AllRoles =
        $"{SuperAdmin},{Executive},{HR},{Employee}";

    // ── Application-specific composites ──────────────────────────────────
    public const string HierarchyManagers =
        $"{SuperAdmin},{Executive},{HR}";

    public const string Viewers =
        $"{SuperAdmin},{Executive},{HR},{Employee}";

    public const string CompanyAdmins =
        $"{SuperAdmin}";
}
