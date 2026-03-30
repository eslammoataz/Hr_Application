using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using HrSystemApp.Domain.Models;
using Microsoft.AspNetCore.Identity;

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

    // Phase 1
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<CompanyLocation> CompanyLocations => Set<CompanyLocation>();
    public DbSet<CompanyHierarchyPosition> CompanyHierarchyPositions => Set<CompanyHierarchyPosition>();
    public DbSet<Employee> Employees => Set<Employee>();

    public DbSet<ContactAdminRequest> ContactAdminRequests => Set<ContactAdminRequest>();

    // Phase 2
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<Team> Teams => Set<Team>();

    // Phase 4
    public DbSet<LeaveBalance> LeaveBalances => Set<LeaveBalance>();
    public DbSet<ProfileUpdateRequest> ProfileUpdateRequests => Set<ProfileUpdateRequest>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    // Requests Feature
    public DbSet<RequestDefinition> RequestDefinitions => Set<RequestDefinition>();
    public DbSet<RequestWorkflowStep> RequestWorkflowSteps => Set<RequestWorkflowStep>();
    public DbSet<Request> Requests => Set<Request>();
    public DbSet<RequestApprovalHistory> RequestApprovalHistory => Set<RequestApprovalHistory>();
    public DbSet<RequestAttachment> RequestAttachments => Set<RequestAttachment>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Configure ApplicationUser
        builder.Entity<ApplicationUser>(entity => { entity.HasIndex(e => e.PhoneNumber).IsUnique(); });


        builder.Entity<ApplicationUser>(b => b.ToTable("Users"));
        builder.Entity<IdentityRole>(b => b.ToTable("Roles"));
        builder.Entity<IdentityUserRole<string>>(b => b.ToTable("UserRoles"));
        builder.Entity<IdentityUserClaim<string>>(b => b.ToTable("UserClaims"));
        builder.Entity<IdentityUserLogin<string>>(b => b.ToTable("UserLogins"));
        builder.Entity<IdentityUserToken<string>>(b => b.ToTable("UserTokens"));
        builder.Entity<IdentityRoleClaim<string>>(b => b.ToTable("RoleClaims"));


        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        HandleEntityStateChanges();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        HandleEntityStateChanges();
        return base.SaveChanges();
    }

    private void HandleEntityStateChanges()
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                    entry.Entity.IsDeleted = false;
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    entry.Property(nameof(BaseEntity.CreatedAt)).IsModified = false;
                    break;

                case EntityState.Deleted:
                    entry.State = EntityState.Modified;
                    entry.Entity.IsDeleted = true;
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    break;
            }
        }

        // Protect CreatedById from being overwritten on updates
        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Property(nameof(AuditableEntity.CreatedById)).IsModified = false;
            }
        }
    }
}
