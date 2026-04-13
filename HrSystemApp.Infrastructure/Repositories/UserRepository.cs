using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using HrSystemApp.Application.Interfaces.Repositories;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using HrSystemApp.Infrastructure.Data;

namespace HrSystemApp.Infrastructure.Repositories;

/// <summary>
/// User repository implementation
/// </summary>
public class UserRepository : Repository<ApplicationUser>, IUserRepository
{
    private readonly UserManager<ApplicationUser> _userManager;

    public UserRepository(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        : base(context)
    {
        _userManager = userManager;
    }

    public async Task<ApplicationUser?> GetByPhoneNumberAsync(string phoneNumber,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet.FirstOrDefaultAsync(u => u.PhoneNumber == phoneNumber, cancellationToken);
    }

    public async Task<ApplicationUser?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _userManager.FindByEmailAsync(email);
    }

    public async Task<bool> CreateUserAsync(ApplicationUser user, string password, UserRole role,
        CancellationToken cancellationToken = default)
    {
        var result = await _userManager.CreateAsync(user, password);
        if (!result.Succeeded)
            return false;

        // Add user to Identity role
        await _userManager.AddToRoleAsync(user, role.ToString());
        return true;
    }

    public async Task<bool> CheckPasswordAsync(ApplicationUser user, string password)
    {
        return await _userManager.CheckPasswordAsync(user, password);
    }

    public async Task<ApplicationUser?> GetByTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        var userToken = await _context.UserTokens
            .FirstOrDefaultAsync(t => t.Value == token, cancellationToken);

        if (userToken is null) return null;

        return await _dbSet.FirstOrDefaultAsync(u => u.Id == userToken.UserId, cancellationToken);
    }

    public async Task SaveTokenAsync(string userId, string token, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user != null)
        {
            await _userManager.SetAuthenticationTokenAsync(user, "Default", "AccessToken", token);
        }
    }

    public async Task<string?> GetTokenAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        return user != null ? await _userManager.GetAuthenticationTokenAsync(user, "Default", "AccessToken") : null;
    }

    public async Task RemoveTokenAsync(string userId, string token, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user != null)
        {
            await _userManager.RemoveAuthenticationTokenAsync(user, "Default", "AccessToken");
        }
    }

    public async Task<bool> ValidateTokenAsync(string userId, string token,
        CancellationToken cancellationToken = default)
    {
        var savedToken = await GetTokenAsync(userId, cancellationToken);
        return savedToken == token;
    }

    public override async Task<ApplicationUser> AddAsync(ApplicationUser entity,
        CancellationToken cancellationToken = default)
    {
        var result = await _userManager.CreateAsync(entity);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to create user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }

        return entity;
    }

    public override async Task UpdateAsync(ApplicationUser entity, CancellationToken cancellationToken = default)
    {
        await _userManager.UpdateAsync(entity);
    }

    public override async Task DeleteAsync(ApplicationUser entity, CancellationToken cancellationToken = default)
    {
        await _userManager.DeleteAsync(entity);
    }

    public async Task<IList<string>> GetRolesAsync(ApplicationUser user)
    {
        return await _userManager.GetRolesAsync(user);
    }

    public async Task<Dictionary<string, string>> GetPrimaryRolesByUserIdsAsync(
        IEnumerable<string> userIds,
        CancellationToken cancellationToken = default)
    {
        var ids = userIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .ToList();

        if (ids.Count == 0)
        {
            return new Dictionary<string, string>();
        }

        var userRoles = await (
            from userRole in _context.Set<IdentityUserRole<string>>().AsNoTracking()
            join role in _context.Roles.AsNoTracking() on userRole.RoleId equals role.Id
            where ids.Contains(userRole.UserId)
            orderby userRole.UserId, role.Name
            select new
            {
                userRole.UserId,
                RoleName = role.Name
            })
            .ToListAsync(cancellationToken);

        return userRoles
            .GroupBy(x => x.UserId)
            .ToDictionary(
                x => x.Key,
                x => x.Select(v => v.RoleName).FirstOrDefault() ?? string.Empty);
    }

    public async Task<(bool Succeeded, IEnumerable<string> Errors)> ChangePasswordAsync(ApplicationUser user,
        string currentPassword, string newPassword)
    {
        var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        return (result.Succeeded, result.Errors.Select(e => e.Description));
    }

    public async Task<string> GenerateUserTokenAsync(ApplicationUser user, string provider, string purpose)
    {
        return await _userManager.GenerateUserTokenAsync(user, provider, purpose);
    }

    public async Task<bool> VerifyUserTokenAsync(ApplicationUser user, string provider, string purpose, string token)
    {
        return await _userManager.VerifyUserTokenAsync(user, provider, purpose, token);
    }

    public async Task<string> GeneratePasswordResetTokenAsync(ApplicationUser user)
    {
        return await _userManager.GeneratePasswordResetTokenAsync(user);
    }

    public async Task<(bool Succeeded, IEnumerable<string> Errors)> ResetPasswordAsync(ApplicationUser user,
        string token, string newPassword)
    {
        var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
        return (result.Succeeded, result.Errors.Select(e => e.Description));
    }

    public async Task<(bool Succeeded, IEnumerable<string> Errors)> SetPasswordAsync(ApplicationUser user,
        string newPassword)
    {
        var hasPassword = await _userManager.HasPasswordAsync(user);
        if (hasPassword)
        {
            var removeResult = await _userManager.RemovePasswordAsync(user);
            if (!removeResult.Succeeded)
            {
                return (false, removeResult.Errors.Select(e => e.Description));
            }
        }

        var addResult = await _userManager.AddPasswordAsync(user, newPassword);
        return (addResult.Succeeded, addResult.Errors.Select(e => e.Description));
    }

    public async Task<ApplicationUser?> GetByEmailWithDetailsAsync(string email,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(u => u.Employee)
            .ThenInclude(e => e.Company)
            .FirstOrDefaultAsync(u => u.NormalizedEmail == email.ToUpper(), cancellationToken);
    }

    public async Task<bool> AddToRoleAsync(ApplicationUser user, string role, CancellationToken cancellationToken = default)
    {
        if (await _userManager.IsInRoleAsync(user, role))
            return true;

        var result = await _userManager.AddToRoleAsync(user, role);
        return result.Succeeded;
    }

    public async Task<bool> RemoveFromRoleAsync(ApplicationUser user, string role,
        CancellationToken cancellationToken = default)
    {
        if (!await _userManager.IsInRoleAsync(user, role))
            return true;

        var result = await _userManager.RemoveFromRoleAsync(user, role);
        return result.Succeeded;
    }
}
