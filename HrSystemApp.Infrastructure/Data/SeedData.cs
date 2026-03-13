
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace HrSystemApp.Infrastructure.Data;

public static class SeedData
{
    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        var userManager = serviceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        // Create SuperAdmin if not exists
        const string superAdminEmail = "superadmin@hrms.com";

        var existingAdmin = await userManager.FindByEmailAsync(superAdminEmail);

        if (existingAdmin == null)
        {
            var superAdmin = new ApplicationUser
            {
                UserName = superAdminEmail,
                Email = superAdminEmail,
                EmailConfirmed = true,
                Role = UserRole.SuperAdmin,
                IsActive = true,
                EmployeeId = null   // SuperAdmin has NO employee record
            };

            var result = await userManager.CreateAsync(superAdmin, "SuperAdmin@123");

            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                throw new Exception($"Failed to seed SuperAdmin: {errors}");
            }
        }
    }
}