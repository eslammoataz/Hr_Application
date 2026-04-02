
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

using Microsoft.Extensions.Configuration;

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
        var superAdminSettings = new HrSystemApp.Application.Settings.SuperAdminSettings();
        configuration.GetSection("SuperAdminSettings").Bind(superAdminSettings);

        var superAdminEmail = string.IsNullOrEmpty(superAdminSettings.Email) ? "superadmin@hrms.com" : superAdminSettings.Email;
        var superAdminPassword = string.IsNullOrEmpty(superAdminSettings.Password) ? "SuperAdmin@123" : superAdminSettings.Password;

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
    }
}