using HrSystemApp.Application.Settings;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrgRole = HrSystemApp.Domain.Enums.OrgRole;

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

        var superAdminEmail = configuration.GetSection("SuperAdminSettings:Email").Value ?? "superadmin@hrms.com";
        var superAdminPassword = configuration.GetSection("SuperAdminSettings:Password").Value ?? "SuperAdmin@123";

        await SeedRolesAsync(roleManager, logger);
        await SeedSuperAdminAsync(userManager, superAdminEmail, superAdminPassword, logger);

        var company = await SeedCompanyAsync(context, logger);
        var location = await SeedLocationAsync(context, company, logger);

        await SeedHierarchyPositionsAsync(context, company, logger);
        await SeedCompanyAdminAsync(userManager, configuration, context, company, location, logger);
        var employees = await SeedOrganizationalHierarchyAsync(userManager, configuration, context, company, location, logger);
        await SeedOrgNodeHierarchyAsync(context, employees, logger);
        await HardenExistingAccountsAsync(context, logger);

        if (environment.IsDevelopment())
            LogSeededAccounts(logger, configuration, superAdminPassword);
    }

    // -------------------------------------------------------------------------

    private static async Task SeedHierarchyPositionsAsync(
        ApplicationDbContext context,
        Company company,
        ILogger logger)
    {
        if (await context.CompanyHierarchyPositions.AnyAsync(p => p.CompanyId == company.Id))
            return;

        var positions = new[]
        {
            (Role: UserRole.Executive, Title: "Executive", Order: 1),
            (Role: UserRole.HR, Title: "Human Resources", Order: 2),
            (Role: UserRole.Employee, Title: "Employee", Order: 3)
        };

        foreach (var p in positions)
        {
            context.CompanyHierarchyPositions.Add(new CompanyHierarchyPosition
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                Role = p.Role,
                PositionTitle = p.Title,
                SortOrder = p.Order
            });
        }

        await context.SaveChangesAsync();
        logger.LogInformation("Hierarchy positions seeded for {Company}.", company.CompanyName);
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
        string email,
        string password,
        ILogger logger)
    {
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

    private static async Task HardenExistingAccountsAsync(ApplicationDbContext context, ILogger logger)
    {
        var seededEmails = new[]
        {
            "superadmin@hrms.com",
            "companyadmin@hrms.com",
            "ceo@hrms.com",
            "vp.eng@hrms.com",
            "manager.eng@hrms.com",
            "ul.softdev@hrms.com",
            "tl.backend@hrms.com",
            "dev.charlie@hrms.com",
            "vp.mark@hrms.com",
            "manager.mark@hrms.com",
            "ul.content@hrms.com",
            "tl.social@hrms.com",
            "emp.fiona@hrms.com",
            "ul.digital@hrms.com",
            "tl.search@hrms.com",
            "emp.harvey@hrms.com",
            "ul.brand@hrms.com",
            "tl.visual@hrms.com",
            "emp.jack@hrms.com"
        };

        var count = await context.Users
            .Where(u => !u.MustChangePassword && !seededEmails.Contains(u.UserName!))
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.MustChangePassword, true));

        if (count > 0)
        {
            logger.LogInformation("Security Hardening: {Count} legacy accounts flagged for mandatory password reset.", count);
        }
    }

    // -------------------------------------------------------------------------

    private static async Task SeedCompanyAdminAsync(
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        ApplicationDbContext context,
        Company company,
        CompanyLocation location,
        ILogger logger)
    {
        var seedPassword = configuration["SeedPasswordSettings"] ?? Guid.NewGuid().ToString();
        var email = "companyadmin@hrms.com";
        var name = "Company Admin";
        var phone = "01000000000";

        var admin = await CreateHierarchyUserAsync(userManager, configuration, context, company, location, null,
            name, UserRole.Executive.ToString(), email, phone, logger);

        if (admin != null)
        {
            logger.LogInformation("CompanyAdmin created successfully for {Company}.", company.CompanyName);
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
    private static async Task<Dictionary<string, Employee>> SeedOrganizationalHierarchyAsync(
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        ApplicationDbContext context,
        Company company,
        CompanyLocation location,
        ILogger logger)
    {
        var employees = new Dictionary<string, Employee>();

        // 1. CEO (Executive)
        var ceo = await CreateHierarchyUserAsync(userManager, configuration, context, company, location, null,
            "John Doe", UserRole.Executive.ToString(), "ceo@hrms.com", "1111111111", logger);
        if (ceo != null) employees["ceo"] = ceo;

        if (ceo == null) return employees;

        // 2. HR (Reports to CEO)
        var hr = await CreateHierarchyUserAsync(userManager, configuration, context, company, location, ceo.Id,
            "Jane Smith", UserRole.HR.ToString(), "hr@hrms.com", "2222222222", logger);
        if (hr != null) employees["hr"] = hr;

        // 3. Staff Employee (Reports to HR)
        var charlie = await CreateHierarchyUserAsync(userManager, configuration, context, company, location, hr?.Id,
            "Charlie Davis", UserRole.Employee.ToString(), "dev.charlie@hrms.com", "6666666666", logger);
        if (charlie != null) employees["charlie"] = charlie;

        // 4. Another Employee (Reports to HR)
        var fiona = await CreateHierarchyUserAsync(userManager, configuration, context, company, location, hr?.Id,
            "Fiona Gallagher", UserRole.Employee.ToString(), "emp.fiona@hrms.com", "7755555555", logger);
        if (fiona != null) employees["fiona"] = fiona;

        logger.LogInformation("Full organizational hierarchy (Multi-Dept) seeded for {Company}.", company.CompanyName);

        return employees;
    }

    private static async Task SeedOrgNodeHierarchyAsync(
        ApplicationDbContext context,
        Dictionary<string, Employee> employees,
        ILogger logger)
    {
        if (employees.Count == 0) return;

        // Check if already seeded
        if (await context.OrgNodes.AnyAsync())
        {
            logger.LogInformation("OrgNode hierarchy already exists, skipping.");
            return;
        }

        var ceo = employees["ceo"];
        var hr = employees["hr"];

        // Create org node hierarchy
        var rootNode = new OrgNode
        {
            Id = Guid.NewGuid(),
            Name = "HRMS Company",
            Type = "company",
            ParentId = null
        };

        var hrNode = new OrgNode
        {
            Id = Guid.NewGuid(),
            Name = "Human Resources",
            Type = "department",
            ParentId = rootNode.Id
        };

        var engNode = new OrgNode
        {
            Id = Guid.NewGuid(),
            Name = "Engineering",
            Type = "division",
            ParentId = rootNode.Id
        };

        var backendTeam = new OrgNode
        {
            Id = Guid.NewGuid(),
            Name = "Backend Team",
            Type = "team",
            ParentId = engNode.Id
        };

        context.OrgNodes.AddRange(rootNode, hrNode, engNode, backendTeam);
        await context.SaveChangesAsync();

        // Assign employees to nodes
        var assignments = new List<OrgNodeAssignment>
        {
            new() { Id = Guid.NewGuid(), OrgNodeId = rootNode.Id, EmployeeId = ceo.Id, Role = OrgRole.Manager },
            new() { Id = Guid.NewGuid(), OrgNodeId = hrNode.Id, EmployeeId = hr.Id, Role = OrgRole.Manager },
        };

        if (employees.TryGetValue("charlie", out var charlie))
        {
            assignments.Add(new OrgNodeAssignment { Id = Guid.NewGuid(), OrgNodeId = backendTeam.Id, EmployeeId = charlie.Id, Role = OrgRole.Member });
        }

        if (employees.TryGetValue("fiona", out var fiona))
        {
            assignments.Add(new OrgNodeAssignment { Id = Guid.NewGuid(), OrgNodeId = backendTeam.Id, EmployeeId = fiona.Id, Role = OrgRole.Member });
        }

        context.OrgNodeAssignments.AddRange(assignments);
        await context.SaveChangesAsync();

        logger.LogInformation("OrgNode hierarchy seeded with {NodeCount} nodes and {AssignmentCount} assignments.",
            4, assignments.Count);
    }

    private static async Task<Employee?> CreateHierarchyUserAsync(
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        ApplicationDbContext context,
        Company company,
        CompanyLocation location,
        Guid? managerId,
        string name,
        string role,
        string email,
        string phone,
        ILogger logger)
    {
        var existingUser = await userManager.FindByEmailAsync(email);
        if (existingUser != null)
        {
            return await context.Employees.FirstOrDefaultAsync(e => e.UserId == existingUser.Id);
        }

        var employee = new Employee
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            ManagerId = managerId,
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

        var seedPassword = configuration["SeedPasswordSettings"] ?? "Pass@123456";
        var result = await userManager.CreateAsync(user, seedPassword);
        if (!result.Succeeded)
        {
            logger.LogWarning("Failed to create user {Email}: {Errors}", email, string.Join(", ", result.Errors.Select(e => e.Description)));
            return null;
        }

        await userManager.AddToRoleAsync(user, role);
        employee.UserId = user.Id;
        await context.SaveChangesAsync();

        return employee;
    }


    // -------------------------------------------------------------------------
    private static void LogSeededAccounts(ILogger logger, IConfiguration configuration, string superAdminPassword)
    {
        var seedPassword = configuration["SeedPasswordSettings"] ?? "Pass@123456";

        logger.LogInformation("============================================================");
        logger.LogInformation("              SEEDED ACCOUNTS SUMMARY");
        logger.LogInformation("============================================================");
        logger.LogInformation("{Role,-20} {Email,-35} {Password}", "Role", "Email", "Password");

        var accounts = new[]
        {
            ("SuperAdmin",        "superadmin@hrms.com",      superAdminPassword),
            ("CompanyAdmin",      "companyadmin@hrms.com",    seedPassword),
            ("CEO",               "ceo@hrms.com",             seedPassword),
            ("VicePresident",     "vp.eng@hrms.com",          seedPassword),
            ("DepartmentManager", "manager.eng@hrms.com",     seedPassword),
            ("UnitLeader",        "ul.softdev@hrms.com",      seedPassword),
            ("TeamLeader",        "tl.backend@hrms.com",       seedPassword),
            ("Employee",          "dev.charlie@hrms.com",      seedPassword),
            ("VicePresident",     "vp.mark@hrms.com",          seedPassword),
            ("DepartmentManager", "manager.mark@hrms.com",     seedPassword),
            ("UnitLeader",        "ul.content@hrms.com",       seedPassword),
            ("TeamLeader",        "tl.social@hrms.com",        seedPassword),
            ("Employee",          "emp.fiona@hrms.com",        seedPassword),
            ("UnitLeader",        "ul.digital@hrms.com",       seedPassword),
            ("TeamLeader",        "tl.search@hrms.com",        seedPassword),
            ("Employee",          "emp.harvey@hrms.com",       seedPassword),
            ("UnitLeader",        "ul.brand@hrms.com",        seedPassword),
            ("TeamLeader",        "tl.visual@hrms.com",        seedPassword),
            ("Employee",          "emp.jack@hrms.com",         seedPassword),
        };

        foreach (var (role, email, pwd) in accounts)
            logger.LogInformation("{Role,-20} {Email,-35} {Password}", role, email, pwd);

        logger.LogInformation("============================================================");
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