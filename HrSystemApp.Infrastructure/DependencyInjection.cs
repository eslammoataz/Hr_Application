using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Repositories;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Models;
using HrSystemApp.Infrastructure.Data;
using HrSystemApp.Infrastructure.Repositories;
using HrSystemApp.Infrastructure.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using HrSystemApp.Application.Settings;
using HrSystemApp.Application.Settings;

namespace HrSystemApp.Infrastructure;

/// <summary>
/// Infrastructure layer dependency injection configuration
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var dataProtectionKeysPath = configuration["DataProtection:KeysPath"];
        var dataProtectionAppName = configuration["DataProtection:ApplicationName"] ?? "HrSystemApp";

        // Persist keys so Identity tokens remain valid across restarts/replicas.
        var dataProtectionBuilder = services.AddDataProtection()
            .SetApplicationName(dataProtectionAppName);

        if (!string.IsNullOrWhiteSpace(dataProtectionKeysPath))
        {
            dataProtectionBuilder.PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));
        }

        // Database
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                b => b.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)));

        // Identity
        services.AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredLength = 6;
                options.User.RequireUniqueEmail = false;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        services.Configure<DataProtectionTokenProviderOptions>(options =>
        {
            options.TokenLifespan = TimeSpan.FromMinutes(15);
        });


        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IEmployeeRepository, EmployeeRepository>();
        services.AddScoped<ICompanyRepository, CompanyRepository>();
        services.AddScoped<ICompanyLocationRepository, CompanyLocationRepository>();
        services.AddScoped<IDepartmentRepository, DepartmentRepository>();
        services.AddScoped<IUnitRepository, UnitRepository>();
        services.AddScoped<ITeamRepository, TeamRepository>();
        services.AddScoped<ILeaveBalanceRepository, LeaveBalanceRepository>();
        services.AddScoped<IContactAdminRequestRepository, ContactAdminRequestRepository>();
        services.AddScoped<IProfileUpdateRequestRepository, ProfileUpdateRequestRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IRequestDefinitionRepository, RequestDefinitionRepository>();
        services.AddScoped<IRequestRepository, RequestRepository>();
        services.AddScoped<ICompanyHierarchyPositionRepository, CompanyHierarchyPositionRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.Configure<EmailSettings>(configuration.GetSection("EmailSettings"));
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<ISmsService, SmsService>();

        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IWorkflowService, WorkflowService>();
        services.AddScoped<IRequestSchemaValidator, RequestSchemaValidator>();
        services.AddScoped<IHierarchyService, HierarchyService>();

        return services;
    }

    /// <summary>
    /// Apply pending migrations automatically
    /// </summary>
    public static async Task ApplyMigrationsAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await context.Database.MigrateAsync();
    }
}
