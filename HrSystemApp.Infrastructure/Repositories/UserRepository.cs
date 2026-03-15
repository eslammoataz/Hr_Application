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

    public async Task<ApplicationUser?> GetByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FirstOrDefaultAsync(u => u.PhoneNumber == phoneNumber, cancellationToken);
    }

    public async Task<ApplicationUser?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _userManager.FindByEmailAsync(email);
    }

    public async Task<bool> CreateUserAsync(ApplicationUser user, string password, UserRole role, CancellationToken cancellationToken = default)
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

    public async Task<bool> ValidateTokenAsync(string userId, string token, CancellationToken cancellationToken = default)
    {
        var savedToken = await GetTokenAsync(userId, cancellationToken);
        return savedToken == token;
    }

    public override async Task<ApplicationUser> AddAsync(ApplicationUser entity, CancellationToken cancellationToken = default)
    {
        var result = await _userManager.CreateAsync(entity);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Failed to create user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
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
}
