
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace HrSystemApp.Infrastructure.Data;

public static class SeedData
{
    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = serviceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

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
        const string superAdminEmail = "superadmin@hrms.com";

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
                EmployeeId = null   // SuperAdmin has NO employee record
            };

            var result = await userManager.CreateAsync(superAdmin, "SuperAdmin@123");

            if (result.Succeeded)
            {
                // Add SuperAdmin to Identity role
                await userManager.AddToRoleAsync(superAdmin, UserRole.SuperAdmin.ToString());
            }
            else
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                throw new Exception($"Failed to seed SuperAdmin: {errors}");
            }
        }
    }
}