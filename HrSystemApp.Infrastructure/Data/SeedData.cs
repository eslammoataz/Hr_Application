
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HrSystemApp.Infrastructure.Data;

public static class SeedData
{
    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = serviceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();

        // Seed Identity roles from UserRole enum
        foreach (UserRole role in Enum.GetValues(typeof(UserRole)))
        {
            var roleName = role.ToString();
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new IdentityRole(roleName));
            }
        }

        // Create SuperAdmin if not exists
        var superAdminSettings = new Application.Settings.SuperAdminSettings();
        configuration.GetSection("SuperAdminSettings").Bind(superAdminSettings);

        var superAdminEmail = string.IsNullOrEmpty(superAdminSettings.Email)
            ? "superadmin@hrms.com"
            : superAdminSettings.Email;
        var superAdminPassword = string.IsNullOrEmpty(superAdminSettings.Password)
            ? "SuperAdmin@123"
            : superAdminSettings.Password;

        var existingAdmin = await userManager.FindByEmailAsync(superAdminEmail);

        if (existingAdmin == null)
        {
            var superAdmin = new ApplicationUser
            {
                UserName = superAdminEmail,
                Email = superAdminEmail,
                Name = "Super Admin",
                EmailConfirmed = true,
                IsActive = true,
                MustChangePassword = false,
                EmployeeId = null // SuperAdmin has NO employee record
            };

            var result = await userManager.CreateAsync(superAdmin, superAdminPassword);

            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(superAdmin, UserRole.SuperAdmin.ToString());
            }
            else
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                throw new Exception($"Failed to seed SuperAdmin: {errors}");
            }
        }
        else
        {
            // User already exists — ensure they have the Identity role (handles migration from old Role column)
            var hasRole = await userManager.IsInRoleAsync(existingAdmin, UserRole.SuperAdmin.ToString());
            if (!hasRole)
                await userManager.AddToRoleAsync(existingAdmin, UserRole.SuperAdmin.ToString());
        }

        var context = serviceProvider.GetRequiredService<ApplicationDbContext>();

        // Seed a target Company
        var companyName = "HRMS";
        var company = context.Companies.FirstOrDefault(c => c.CompanyName == companyName);
        if (company == null)
        {
            company = new Company
            {
                Id = Guid.NewGuid(),
                CompanyName = companyName,
                YearlyVacationDays = 21,
                StartTime = new TimeSpan(9, 0, 0),
                EndTime = new TimeSpan(17, 0, 0),
                GraceMinutes = 15,
                TimeZoneId = "UTC",
                Status = CompanyStatus.Active,
                CreatedAt = DateTime.UtcNow
            };
            context.Companies.Add(company);

            var location = new CompanyLocation
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                LocationName = "Main Branch",
                Address = "123 Innovation Drive",
                CreatedAt = DateTime.UtcNow
            };
            context.CompanyLocations.Add(location);

            var department = new Department
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                Name = "Engineering",
                CreatedAt = DateTime.UtcNow
            };
            context.Departments.Add(department);

            await context.SaveChangesAsync();

            async Task SeedEmployeeUser(string name, string roleStr, string password, string emailPrefix, string phoneNumber)
            {
                var email = $"{emailPrefix}@hrms.com";
                if (await userManager.FindByEmailAsync(email) == null)
                {
                    var employee = new Employee
                    {
                        Id = Guid.NewGuid(),
                        CompanyId = company.Id,
                        DepartmentId = department.Id,
                        CompanyLocationId = location.Id,
                        Email = email,
                        FullName = name,
                        PhoneNumber = phoneNumber,
                        EmployeeCode = "EMP" + new Random().Next(1000, 9999),
                        EmploymentStatus = EmploymentStatus.Active
                    };
                    context.Employees.Add(employee);
                    await context.SaveChangesAsync();

                    var user = new ApplicationUser
                    {
                        UserName = email,
                        Email = email,
                        Name = name,
                        PhoneNumber = phoneNumber,
                        EmailConfirmed = true,
                        IsActive = true,
                        MustChangePassword = false,
                        EmployeeId = employee.Id
                    };
                    var res = await userManager.CreateAsync(user, password);
                    if (res.Succeeded)
                    {
                        await userManager.AddToRoleAsync(user, roleStr);
                        employee.UserId = user.Id;
                        await context.SaveChangesAsync();
                    }
                }
            }

            await SeedEmployeeUser("Admin User", nameof(UserRole.CompanyAdmin), "Pass@123", "admin", "1234567890");
            await SeedEmployeeUser("HR User", nameof(UserRole.HR), "Pass@123", "hr", "1234567891");

            for (int i = 1; i <= 4; i++)
            {
                await SeedEmployeeUser($"Employee {i}", nameof(UserRole.Employee), "Pass@123", $"emp.{i}", $"123456789{i + 1}");
            }
        }
        else
        {
            var needsUpdate = false;
            if (company.StartTime == default)
            {
                company.StartTime = new TimeSpan(9, 0, 0);
                needsUpdate = true;
            }

            if (company.EndTime == default)
            {
                company.EndTime = new TimeSpan(17, 0, 0);
                needsUpdate = true;
            }

            if (company.GraceMinutes <= 0)
            {
                company.GraceMinutes = 15;
                needsUpdate = true;
            }

            if (string.IsNullOrWhiteSpace(company.TimeZoneId))
            {
                company.TimeZoneId = "UTC";
                needsUpdate = true;
            }

            if (needsUpdate)
            {
                await context.SaveChangesAsync();
            }
        }

        var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("SeedData");
        logger.LogInformation("================== TEST CREDENTIALS ==================");
        logger.LogInformation($"SuperAdmin   => Email: {superAdminEmail} | Password: {superAdminPassword}");
        logger.LogInformation($"CompanyAdmin => Email: admin@hrms.com | Password: Pass@123 | Phone: 1234567890");
        logger.LogInformation($"HR Manager   => Email: hr@hrms.com | Password: Pass@123 | Phone: 1234567891");
        for (int i = 1; i <= 4; i++)
        {
            logger.LogInformation($"Employee {i}   => Email: emp.{i}@hrms.com | Password: Pass@123 | Phone: 123456789{i + 1}");
        }
        logger.LogInformation("======================================================");
    }
}
