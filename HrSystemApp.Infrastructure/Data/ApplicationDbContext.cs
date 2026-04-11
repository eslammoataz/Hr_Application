using HrSystemApp.Domain.Common;
using HrSystemApp.Domain.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace HrSystemApp.Infrastructure.Data;

/// <summary>
/// Application database context
/// </summary>
public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    /// <summary>
    /// Creates a new ApplicationDbContext configured with the provided EF Core options.
    /// </summary>
    /// <param name="options">EF Core configuration options for this DbContext.</param>
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Employee> Employees { get; set; } = null!;
    public DbSet<Department> Departments { get; set; } = null!;
    public DbSet<Unit> Units { get; set; } = null!;
    public DbSet<Team> Teams { get; set; } = null!;
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

    /// <summary>
    /// Configures the EF Core model for this context by applying entity configurations from the current assembly and registering global query filters (including the soft-delete filter).
    /// </summary>
    /// <param name="builder">The ModelBuilder used to configure entity mappings, constraints, and global query filters.</param>
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Apply all configurations from the current assembly
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // Standardize Soft Delete Global Query Filter
        ApplySoftDeleteQueryFilter(builder);
    }

    /// <summary>
    /// Ensures entity audit fields and soft-delete behavior are applied before persisting changes.
    /// </summary>
    /// <returns>The number of state entries written to the database.</returns>
    public override int SaveChanges()
    {
        HandleEntityStateChanges();
        return base.SaveChanges();
    }

    /// <summary>
    /// Applies entity auditing and soft-delete rules, then persists changes to the database.
    /// </summary>
    /// <returns>The number of state entries written to the database.</returns>
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        HandleEntityStateChanges();
        return base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Applies a global query filter to all entity types derived from BaseEntity so that soft-deleted records are excluded from queries.
    /// </summary>
    /// <param name="builder">The EF Core <see cref="Microsoft.EntityFrameworkCore.ModelBuilder"/> used to configure the model.</param>
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

    /// <summary>
    /// Applies a global query filter for the specified BaseEntity-derived type to exclude entities where `IsDeleted` is true.
    /// </summary>
    /// <typeparam name="T">The entity CLR type that inherits from <c>HrSystemApp.Domain.Models.BaseEntity</c>.</typeparam>
    /// <param name="builder">The EF Core <c>ModelBuilder</c> used to configure the entity type.</param>
    private static void SetSoftDeleteFilter<T>(ModelBuilder builder) where T : HrSystemApp.Domain.Models.BaseEntity
    {
        builder.Entity<T>().HasQueryFilter(e => !e.IsDeleted);
    }

    /// <summary>
    /// Enforces auditing fields and soft-delete semantics for tracked BaseEntity instances before persistence.
    /// </summary>
    /// <remarks>
    /// - For added entities: sets CreatedAt to the current UTC time and ensures IsDeleted is false.
    /// - For modified entities: sets UpdatedAt to the current UTC time and prevents CreatedAt from being changed.
    /// - For deleted entities: if the entity implements IHardDelete it is left for hard deletion; otherwise the operation is converted to a soft delete by setting IsDeleted to true and UpdatedAt to the current UTC time.
    /// </remarks>
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
                    entry.State = EntityState.Modified;
                    entry.Entity.IsDeleted = true;
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    break;
            }
        }
    }
}
