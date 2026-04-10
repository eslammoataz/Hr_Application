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
        
        await SeedHierarchyPositionsAsync(context, company, logger);
        await SeedOrganizationalHierarchyAsync(userManager, context, company, location, logger);

        if (environment.IsDevelopment())
            LogTestCredentials(logger, configuration);
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
            (Role: UserRole.CEO, Title: "Chief Executive Officer", Order: 1),
            (Role: UserRole.VicePresident, Title: "Vice President", Order: 2),
            (Role: UserRole.DepartmentManager, Title: "Department Manager", Order: 3),
            (Role: UserRole.UnitLeader, Title: "Unit Leader", Order: 4),
            (Role: UserRole.TeamLeader, Title: "Team Leader", Order: 5),
            (Role: UserRole.Employee, Title: "Employee", Order: 6)
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
    private static async Task SeedOrganizationalHierarchyAsync(
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext context,
        Company company,
        CompanyLocation location,
        ILogger logger)
    {
        // 1. CEO (Root)
        var ceo = await CreateHierarchyUserAsync(userManager, context, company, location, null, null, null, null,
            "John Doe", UserRole.CEO.ToString(), "ceo@hrms.com", "1111111111", logger);

        if (ceo == null) return;

        // 2. VP of Engineering (Reports to CEO)
        var vp = await CreateHierarchyUserAsync(userManager, context, company, location, null, null, null, ceo.Id,
            "Jane Smith", UserRole.VicePresident.ToString(), "vp.eng@hrms.com", "2222222222", logger);

        // 3. Engineering Department (Led by VP)
        var engineering = await context.Departments.FirstOrDefaultAsync(d => d.CompanyId == company.Id && d.Name == "Engineering");
        if (engineering == null)
        {
            engineering = new Department { Id = Guid.NewGuid(), CompanyId = company.Id, Name = "Engineering", VicePresidentId = vp?.Id, CreatedAt = DateTime.UtcNow };
            context.Departments.Add(engineering);
            await context.SaveChangesAsync();
        }

        // 4. Engineering Manager (Reports to VP)
        var engManager = await CreateHierarchyUserAsync(userManager, context, company, location, engineering.Id, null, null, vp?.Id,
            "Robert Brown", UserRole.DepartmentManager.ToString(), "manager.eng@hrms.com", "3333333333", logger);
        
        engineering.ManagerId = engManager?.Id;
        await context.SaveChangesAsync();

        // 5. Software Dev Unit Leader (Reports to Mgr)
        var unitLeader = await CreateHierarchyUserAsync(userManager, context, company, location, engineering.Id, null, null, engManager?.Id,
            "Alice Johnson", UserRole.UnitLeader.ToString(), "ul.softdev@hrms.com", "4444444444", logger);

        // 6. Software Development Unit (Led by UL)
        var softDev = await context.Units.FirstOrDefaultAsync(u => u.DepartmentId == engineering.Id && u.Name == "Software Development");
        if (softDev == null)
        {
            softDev = new Unit { Id = Guid.NewGuid(), DepartmentId = engineering.Id, Name = "Software Development", UnitLeaderId = unitLeader?.Id, CreatedAt = DateTime.UtcNow };
            context.Units.Add(softDev);
            await context.SaveChangesAsync();
        }

        // 7. Backend Team Leader (Reports to UL)
        var teamLeader = await CreateHierarchyUserAsync(userManager, context, company, location, engineering.Id, softDev.Id, null, unitLeader?.Id,
            "Bob Wilson", UserRole.TeamLeader.ToString(), "tl.backend@hrms.com", "5555555555", logger);

        // 8. Backend Team (Led by TL)
        var backendTeam = await context.Teams.FirstOrDefaultAsync(t => t.UnitId == softDev.Id && t.Name == "Backend Team");
        if (backendTeam == null)
        {
            backendTeam = new Team { Id = Guid.NewGuid(), UnitId = softDev.Id, Name = "Backend Team", TeamLeaderId = teamLeader?.Id, CreatedAt = DateTime.UtcNow };
            context.Teams.Add(backendTeam);
            await context.SaveChangesAsync();
        }

        // 9. Staff Developer (Reports to TL)
        await CreateHierarchyUserAsync(userManager, context, company, location, engineering.Id, softDev.Id, backendTeam.Id, teamLeader?.Id,
            "Charlie Davis", UserRole.Employee.ToString(), "dev.charlie@hrms.com", "6666666666", logger);

        // =========================================================================
        // DEPARTMENT 2: CREATIVE & MARKETING (3 UNITS)
        // =========================================================================
        
        // 1. Marketing VP (Reports to CEO)
        var vpMark = await CreateHierarchyUserAsync(userManager, context, company, location, null, null, null, ceo.Id,
            "Mark Stevens", UserRole.VicePresident.ToString(), "vp.mark@hrms.com", "7711111111", logger);

        // 2. Marketing Department (Led by VP)
        var marketing = await context.Departments.FirstOrDefaultAsync(d => d.CompanyId == company.Id && d.Name == "Marketing");
        if (marketing == null)
        {
            marketing = new Department { Id = Guid.NewGuid(), CompanyId = company.Id, Name = "Marketing", VicePresidentId = vpMark?.Id, CreatedAt = DateTime.UtcNow };
            context.Departments.Add(marketing);
            await context.SaveChangesAsync();
        }

        // 3. Marketing Manager (Reports to VP)
        var markManager = await CreateHierarchyUserAsync(userManager, context, company, location, marketing.Id, null, null, vpMark?.Id,
            "Sarah Parker", UserRole.DepartmentManager.ToString(), "manager.mark@hrms.com", "7722222222", logger);
        
        marketing.ManagerId = markManager?.Id;
        await context.SaveChangesAsync();

        // -------------------------------------------------------------------------
        // UNIT 2.1: CONTENT & MEDIA
        var ulContent = await CreateHierarchyUserAsync(userManager, context, company, location, marketing.Id, null, null, markManager?.Id,
            "David Miller", UserRole.UnitLeader.ToString(), "ul.content@hrms.com", "7733333333", logger);

        var contentUnit = await context.Units.FirstOrDefaultAsync(u => u.DepartmentId == marketing.Id && u.Name == "Content & Media");
        if (contentUnit == null)
        {
            contentUnit = new Unit { Id = Guid.NewGuid(), DepartmentId = marketing.Id, Name = "Content & Media", UnitLeaderId = ulContent?.Id, CreatedAt = DateTime.UtcNow };
            context.Units.Add(contentUnit);
            await context.SaveChangesAsync();
        }

        var tlSocial = await CreateHierarchyUserAsync(userManager, context, company, location, marketing.Id, contentUnit.Id, null, ulContent?.Id,
            "Elena Vance", UserRole.TeamLeader.ToString(), "tl.social@hrms.com", "7744444444", logger);

        var socialTeam = await context.Teams.FirstOrDefaultAsync(t => t.UnitId == contentUnit.Id && t.Name == "Social Ops");
        if (socialTeam == null)
        {
            socialTeam = new Team { Id = Guid.NewGuid(), UnitId = contentUnit.Id, Name = "Social Ops", TeamLeaderId = tlSocial?.Id, CreatedAt = DateTime.UtcNow };
            context.Teams.Add(socialTeam);
            await context.SaveChangesAsync();
        }

        await CreateHierarchyUserAsync(userManager, context, company, location, marketing.Id, contentUnit.Id, socialTeam.Id, tlSocial?.Id,
            "Fiona Gallagher", UserRole.Employee.ToString(), "emp.fiona@hrms.com", "7755555555", logger);

        // -------------------------------------------------------------------------
        // UNIT 2.2: DIGITAL PERFORMANCE
        var ulDigital = await CreateHierarchyUserAsync(userManager, context, company, location, marketing.Id, null, null, markManager?.Id,
            "Frank Castle", UserRole.UnitLeader.ToString(), "ul.digital@hrms.com", "7766666666", logger);

        var digitalUnit = await context.Units.FirstOrDefaultAsync(u => u.DepartmentId == marketing.Id && u.Name == "Digital Performance");
        if (digitalUnit == null)
        {
            digitalUnit = new Unit { Id = Guid.NewGuid(), DepartmentId = marketing.Id, Name = "Digital Performance", UnitLeaderId = ulDigital?.Id, CreatedAt = DateTime.UtcNow };
            context.Units.Add(digitalUnit);
            await context.SaveChangesAsync();
        }

        var tlSearch = await CreateHierarchyUserAsync(userManager, context, company, location, marketing.Id, digitalUnit.Id, null, ulDigital?.Id,
            "Gina Torres", UserRole.TeamLeader.ToString(), "tl.search@hrms.com", "7777777777", logger);

        var searchTeam = await context.Teams.FirstOrDefaultAsync(t => t.UnitId == digitalUnit.Id && t.Name == "Growth SEO");
        if (searchTeam == null)
        {
            searchTeam = new Team { Id = Guid.NewGuid(), UnitId = digitalUnit.Id, Name = "Growth SEO", TeamLeaderId = tlSearch?.Id, CreatedAt = DateTime.UtcNow };
            context.Teams.Add(searchTeam);
            await context.SaveChangesAsync();
        }

        await CreateHierarchyUserAsync(userManager, context, company, location, marketing.Id, digitalUnit.Id, searchTeam.Id, tlSearch?.Id,
            "Harvey Specter", UserRole.Employee.ToString(), "emp.harvey@hrms.com", "7788888888", logger);

        // -------------------------------------------------------------------------
        // UNIT 2.3: BRAND IDENTITY
        var ulBrand = await CreateHierarchyUserAsync(userManager, context, company, location, marketing.Id, null, null, markManager?.Id,
            "Henry Cavill", UserRole.UnitLeader.ToString(), "ul.brand@hrms.com", "7799999999", logger);

        var brandUnit = await context.Units.FirstOrDefaultAsync(u => u.DepartmentId == marketing.Id && u.Name == "Brand Identity");
        if (brandUnit == null)
        {
            brandUnit = new Unit { Id = Guid.NewGuid(), DepartmentId = marketing.Id, Name = "Brand Identity", UnitLeaderId = ulBrand?.Id, CreatedAt = DateTime.UtcNow };
            context.Units.Add(brandUnit);
            await context.SaveChangesAsync();
        }

        var tlVisual = await CreateHierarchyUserAsync(userManager, context, company, location, marketing.Id, brandUnit.Id, null, ulBrand?.Id,
            "Iris West", UserRole.TeamLeader.ToString(), "tl.visual@hrms.com", "7700000000", logger);

        var visualTeam = await context.Teams.FirstOrDefaultAsync(t => t.UnitId == brandUnit.Id && t.Name == "Visual Arts");
        if (visualTeam == null)
        {
            visualTeam = new Team { Id = Guid.NewGuid(), UnitId = brandUnit.Id, Name = "Visual Arts", TeamLeaderId = tlVisual?.Id, CreatedAt = DateTime.UtcNow };
            context.Teams.Add(visualTeam);
            await context.SaveChangesAsync();
        }

        await CreateHierarchyUserAsync(userManager, context, company, location, marketing.Id, brandUnit.Id, visualTeam.Id, tlVisual?.Id,
            "Jack Reacher", UserRole.Employee.ToString(), "emp.jack@hrms.com", "7711223344", logger);

        logger.LogInformation("Full organizational hierarchy (Multi-Dept) seeded for {Company}.", company.CompanyName);
    }

    private static async Task<Employee?> CreateHierarchyUserAsync(
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext context,
        Company company,
        CompanyLocation location,
        Guid? departmentId,
        Guid? unitId,
        Guid? teamId,
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
            DepartmentId = departmentId,
            UnitId = unitId,
            TeamId = teamId,
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
            EmployeeId = employee.Id
        };

        var result = await userManager.CreateAsync(user, "Pass@123");
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

    private static void LogTestCredentials(ILogger logger, IConfiguration configuration)
    {
        var settings = configuration.GetSection("SuperAdminSettings").Get<SuperAdminSettings>() ?? new();
        var superEmail = settings.Email.NullIfWhiteSpace() ?? "superadmin@hrms.com";
        var superPassword = settings.Password.NullIfWhiteSpace() ?? "SuperAdmin@123";

        logger.LogInformation("================== TEST CREDENTIALS (HIERARCHY) ==================");
        logger.LogInformation("SuperAdmin     => {Email} / {Password}", superEmail, superPassword);
        logger.LogInformation("CEO            => ceo@hrms.com / Pass@123");
        logger.LogInformation("VP Eng         => vp.eng@hrms.com / Pass@123");
        logger.LogInformation("Manager Eng    => manager.eng@hrms.com / Pass@123");
        logger.LogInformation("Unit Leader    => ul.softdev@hrms.com / Pass@123");
        logger.LogInformation("Team Leader    => tl.backend@hrms.com / Pass@123");
        logger.LogInformation("Developer      => dev.charlie@hrms.com / Pass@123");
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