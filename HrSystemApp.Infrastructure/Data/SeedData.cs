using HrSystemApp.Application.Settings;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HrSystemApp.Infrastructure.Data;

public static class SeedData
{
    private static readonly Random _random = new();

    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        var context = serviceProvider.GetRequiredService<ApplicationDbContext>();
        var environment = serviceProvider.GetRequiredService<IHostEnvironment>();
        var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("SeedData"); // ← fix

        await SeedRolesAsync(roleManager, logger);
        await SeedSuperAdminAsync(userManager, configuration, logger);

        var company = await SeedCompanyAsync(context, logger);
        var location = await SeedLocationAsync(context, company, logger);
        var department = await SeedDepartmentAsync(context, company, logger);

        await SeedEmployeesAsync(userManager, context, company, location, department, logger);

        if (environment.IsDevelopment())
            LogTestCredentials(logger, configuration);
    }

    // -------------------------------------------------------------------------

    private static async Task SeedRolesAsync(
        RoleManager<IdentityRole> roleManager,
        ILogger logger)
    {
        foreach (UserRole role in Enum.GetValues<UserRole>())
        {
            var roleName = role.ToString();
            if (await roleManager.RoleExistsAsync(roleName))
                continue;

            var result = await roleManager.CreateAsync(new IdentityRole(roleName));
            if (!result.Succeeded)
                logger.LogWarning("Failed to create role {Role}: {Errors}", roleName, FormatErrors(result));
        }
    }

    // -------------------------------------------------------------------------

    private static async Task SeedSuperAdminAsync(
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        ILogger logger)
    {
        var settings = configuration
            .GetSection("SuperAdminSettings")
            .Get<SuperAdminSettings>() ?? new SuperAdminSettings();

        var email = settings.Email.NullIfWhiteSpace() ?? "superadmin@hrms.com";
        var password = settings.Password.NullIfWhiteSpace() ?? "SuperAdmin@123";

        var existing = await userManager.FindByEmailAsync(email);
        if (existing is null)
        {
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                Name = "Super Admin",
                EmailConfirmed = true,
                IsActive = true,
                MustChangePassword = false,
                EmployeeId = null
            };

            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                logger.LogError("Failed to create SuperAdmin: {Errors}", FormatErrors(result));
                return;
            }

            await userManager.AddToRoleAsync(user, UserRole.SuperAdmin.ToString());
            logger.LogInformation("SuperAdmin created successfully.");
            return;
        }

        if (!await userManager.IsInRoleAsync(existing, UserRole.SuperAdmin.ToString()))
        {
            await userManager.AddToRoleAsync(existing, UserRole.SuperAdmin.ToString());
            logger.LogInformation("SuperAdmin role assigned to existing user.");
        }
    }

    // -------------------------------------------------------------------------

    private static async Task<Company> SeedCompanyAsync(
        ApplicationDbContext context,
        ILogger logger)
    {
        var company = await context.Companies
            .FirstOrDefaultAsync(c => c.CompanyName == "HRMS");

        if (company is null)
        {
            company = new Company
            {
                Id = Guid.NewGuid(),
                CompanyName = "HRMS",
                YearlyVacationDays = 21,
                StartTime = new TimeSpan(9, 0, 0),
                EndTime = new TimeSpan(17, 0, 0),
                GraceMinutes = 15,
                TimeZoneId = "UTC",
                Status = CompanyStatus.Active,
                CreatedAt = DateTime.UtcNow
            };
            context.Companies.Add(company);
            await context.SaveChangesAsync();
            logger.LogInformation("Company '{Name}' seeded.", company.CompanyName);
        }
        else
        {
            var updated = false;
            if (company.StartTime == default)
            {
                company.StartTime = new TimeSpan(9, 0, 0);
                updated = true;
            }

            if (company.EndTime == default)
            {
                company.EndTime = new TimeSpan(17, 0, 0);
                updated = true;
            }

            if (company.GraceMinutes <= 0)
            {
                company.GraceMinutes = 15;
                updated = true;
            }

            if (company.TimeZoneId.IsNullOrWhiteSpace())
            {
                company.TimeZoneId = "UTC";
                updated = true;
            }

            if (updated)
            {
                await context.SaveChangesAsync();
                logger.LogInformation("Company '{Name}' updated with missing defaults.", company.CompanyName);
            }
        }

        return company;
    }

    // -------------------------------------------------------------------------

    private static async Task<CompanyLocation> SeedLocationAsync(
        ApplicationDbContext context,
        Company company,
        ILogger logger)
    {
        var location = await context.CompanyLocations
            .FirstOrDefaultAsync(l => l.CompanyId == company.Id);

        if (location is not null)
            return location;

        location = new CompanyLocation
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            LocationName = "Main Branch",
            Address = "123 Innovation Drive",
            CreatedAt = DateTime.UtcNow
        };
        context.CompanyLocations.Add(location);
        await context.SaveChangesAsync();
        logger.LogInformation("Location '{Name}' seeded.", location.LocationName);

        return location;
    }

    // -------------------------------------------------------------------------

    private static async Task<Department> SeedDepartmentAsync(
        ApplicationDbContext context,
        Company company,
        ILogger logger)
    {
        var department = await context.Departments
            .FirstOrDefaultAsync(d => d.CompanyId == company.Id);

        if (department is not null)
            return department;

        department = new Department
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            Name = "Engineering",
            CreatedAt = DateTime.UtcNow
        };
        context.Departments.Add(department);
        await context.SaveChangesAsync();
        logger.LogInformation("Department '{Name}' seeded.", department.Name);

        return department;
    }

    // -------------------------------------------------------------------------

    private static async Task SeedEmployeesAsync(
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext context,
        Company company,
        CompanyLocation location,
        Department department,
        ILogger logger)
    {
        var users = new[]
        {
            (Name: "Admin User", Role: UserRole.CompanyAdmin, Email: "admin@hrms.com", Phone: "1234567890"),
            (Name: "HR User", Role: UserRole.HR, Email: "hr@hrms.com", Phone: "1234567891"),
            (Name: "Employee 1", Role: UserRole.Employee, Email: "emp.1@hrms.com", Phone: "1234567892"),
            (Name: "Employee 2", Role: UserRole.Employee, Email: "emp.2@hrms.com", Phone: "1234567893"),
            (Name: "Employee 3", Role: UserRole.Employee, Email: "emp.3@hrms.com", Phone: "1234567894"),
            (Name: "Employee 4", Role: UserRole.Employee, Email: "emp.4@hrms.com", Phone: "1234567895"),
        };

        foreach (var u in users)
        {
            await SeedSingleEmployeeAsync(
                userManager, context,
                company, location, department,
                u.Name, u.Role.ToString(), "Pass@123", u.Email, u.Phone,
                logger);
        }
    }

    // -------------------------------------------------------------------------

    private static async Task SeedSingleEmployeeAsync(
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext context,
        Company company,
        CompanyLocation location,
        Department department,
        string name,
        string role,
        string password,
        string email,
        string phone,
        ILogger logger)
    {
        if (await userManager.FindByEmailAsync(email) is not null)
            return;

        var employee = new Employee
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            DepartmentId = department.Id,
            CompanyLocationId = location.Id,
            Email = email,
            FullName = name,
            PhoneNumber = phone,
            EmployeeCode = $"EMP{_random.Next(1000, 9999)}",
            EmploymentStatus = EmploymentStatus.Active
        };

        context.Employees.Add(employee);
        await context.SaveChangesAsync();

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            Name = name,
            PhoneNumber = phone,
            EmailConfirmed = true,
            IsActive = true,
            MustChangePassword = false,
            EmployeeId = employee.Id
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            logger.LogWarning("Failed to create user {Email}: {Errors}", email, FormatErrors(result));
            return;
        }

        await userManager.AddToRoleAsync(user, role);
        employee.UserId = user.Id;
        await context.SaveChangesAsync();

        logger.LogInformation("Seeded user {Email} with role {Role}.", email, role);
    }

    // -------------------------------------------------------------------------

    private static void LogTestCredentials(ILogger logger, IConfiguration configuration)
    {
        var settings = configuration.GetSection("SuperAdminSettings").Get<SuperAdminSettings>() ?? new();
        var superEmail = settings.Email.NullIfWhiteSpace() ?? "superadmin@hrms.com";
        var superPassword = settings.Password.NullIfWhiteSpace() ?? "SuperAdmin@123";

        logger.LogInformation("================== TEST CREDENTIALS (DEV ONLY) ==================");
        logger.LogInformation("SuperAdmin   => {Email} / {Password}", superEmail, superPassword);
        logger.LogInformation("CompanyAdmin => admin@hrms.com / Pass@123");
        logger.LogInformation("HR Manager   => hr@hrms.com / Pass@123");
        for (int i = 1; i <= 4; i++)
            logger.LogInformation("Employee {Index} => emp.{Index}@hrms.com / Pass@123", i, i);
        logger.LogInformation("==================================================================");
    }

    // -------------------------------------------------------------------------

    private static string FormatErrors(IdentityResult result) =>
        string.Join(", ", result.Errors.Select(e => e.Description));
}

// -------------------------------------------------------------------------

file static class StringExtensions
{
    public static string? NullIfWhiteSpace(this string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    public static bool IsNullOrWhiteSpace(this string? value) =>
        string.IsNullOrWhiteSpace(value);
}