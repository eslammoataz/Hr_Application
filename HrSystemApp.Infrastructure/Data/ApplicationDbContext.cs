using HrSystemApp.Application.Common;
using HrSystemApp.Domain.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using HrSystemApp.Domain.Common; // For IHardDelete

namespace HrSystemApp.Infrastructure.Data;

/// <summary>
/// Application database context
/// </summary>
public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Employee> Employees { get; set; } = null!;
    public DbSet<Attendance> Attendances { get; set; } = null!;
    public DbSet<AttendanceLog> AttendanceLogs { get; set; } = null!;
    public DbSet<AttendanceAdjustment> AttendanceAdjustments { get; set; } = null!;
    public DbSet<AttendanceReminderLog> AttendanceReminderLogs { get; set; } = null!;
    public DbSet<Notification> Notifications { get; set; } = null!;
    public DbSet<RefreshToken> RefreshTokens { get; set; } = null!;
    public DbSet<Company> Companies { get; set; } = null!;
    public DbSet<CompanyLocation> CompanyLocations { get; set; } = null!;
    public DbSet<CompanyHierarchyPosition> CompanyHierarchyPositions { get; set; } = null!;
    public DbSet<LeaveBalance> LeaveBalances { get; set; } = null!;
    public DbSet<ContactAdminRequest> ContactAdminRequests { get; set; } = null!;
    public DbSet<ProfileUpdateRequest> ProfileUpdateRequests { get; set; } = null!;
    public DbSet<RequestDefinition> RequestDefinitions { get; set; } = null!;
    public DbSet<Request> Requests { get; set; } = null!;
    public DbSet<OrgNode> OrgNodes { get; set; } = null!;
    public DbSet<OrgNodeAssignment> OrgNodeAssignments { get; set; } = null!;
    public DbSet<CompanyRole> CompanyRoles { get; set; } = null!;
    public DbSet<CompanyRolePermission> CompanyRolePermissions { get; set; } = null!;
    public DbSet<EmployeeCompanyRole> EmployeeCompanyRoles { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Map Identity tables to custom names to match existing migrations
        builder.Entity<ApplicationUser>().ToTable("Users");
        builder.Entity<IdentityRole>().ToTable("Roles");
        builder.Entity<IdentityUserRole<string>>().ToTable("UserRoles");
        builder.Entity<IdentityUserClaim<string>>().ToTable("UserClaims");
        builder.Entity<IdentityUserLogin<string>>().ToTable("UserLogins");
        builder.Entity<IdentityRoleClaim<string>>().ToTable("RoleClaims");
        builder.Entity<IdentityUserToken<string>>().ToTable("UserTokens");

        // Apply all configurations from the current assembly
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // Standardize Soft Delete Global Query Filter
        ApplySoftDeleteQueryFilter(builder);
    }

    public override int SaveChanges()
    {
        HandleEntityStateChanges();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        HandleEntityStateChanges();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void ApplySoftDeleteQueryFilter(ModelBuilder builder)
    {
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            // Apply to any entity that inherits from our Domain BaseEntity (soft-delete capable)
            if (typeof(HrSystemApp.Domain.Models.BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                var method = typeof(ApplicationDbContext)
                    .GetMethod(nameof(SetSoftDeleteFilter), BindingFlags.NonPublic | BindingFlags.Static)
                    ?.MakeGenericMethod(entityType.ClrType);
                method?.Invoke(null, new object[] { builder });
            }
        }
    }

    private static void SetSoftDeleteFilter<T>(ModelBuilder builder) where T : HrSystemApp.Domain.Models.BaseEntity
    {
        builder.Entity<T>().HasQueryFilter(e => !e.IsDeleted);
    }

    private void HandleEntityStateChanges()
    {
        foreach (var entry in ChangeTracker.Entries<HrSystemApp.Domain.Models.BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                    entry.Entity.IsDeleted = false;
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    // Protect CreatedAt from being overwritten
                    entry.Property(nameof(HrSystemApp.Domain.Models.BaseEntity.CreatedAt)).IsModified = false;
                    break;

                case EntityState.Deleted:
                    // Hard delete for entities implementing IHardDelete
                    if (entry.Entity is IHardDelete)
                        break;

                    // Standard soft-delete behavior
                    entry.Property("IsDeleted").CurrentValue = true;
                    entry.Property("DeletedAt").CurrentValue = DateTime.UtcNow;
                    entry.State = EntityState.Modified;
                    break;
            }
        }
    }
}
