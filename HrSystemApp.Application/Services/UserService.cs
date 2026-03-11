using Microsoft.Extensions.Logging;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.User;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Models;

namespace HrSystemApp.Application.Services;

/// <summary>
/// User service implementation
/// </summary>
public class UserService : IUserService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UserService> _logger;

    public UserService(IUnitOfWork unitOfWork, ILogger<UserService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<UserDto>> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(id, cancellationToken);

        if (user is null)
        {
            return Result.Failure<UserDto>(DomainErrors.User.NotFound);
        }

        return Result.Success(MapToDto(user));
    }

    public async Task<Result<UserDto>> GetByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Users.GetByPhoneNumberAsync(phoneNumber, cancellationToken);

        if (user is null)
        {
            return Result.Failure<UserDto>(DomainErrors.User.NotFound);
        }

        return Result.Success(MapToDto(user));
    }

    public async Task<Result<PagedResult<UserDto>>> GetAllAsync(PaginationParams pagination, CancellationToken cancellationToken = default)
    {
        var users = await _unitOfWork.Users.GetAllAsync(cancellationToken);
        var totalCount = users.Count;

        var pagedUsers = users
            .Skip((pagination.PageNumber - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .Select(MapToDto)
            .ToList();

        return Result.Success(PagedResult<UserDto>.Create(
            pagedUsers,
            pagination.PageNumber,
            pagination.PageSize,
            totalCount
        ));
    }

    public async Task<Result<UserDto>> UpdateAsync(string id, UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(id, cancellationToken);

        if (user is null)
        {
            return Result.Failure<UserDto>(DomainErrors.User.NotFound);
        }

        if (request.Name is not null) user.Name = request.Name;
        if (request.Email is not null) user.Email = request.Email;
        if (request.Country is not null) user.Country = request.Country;
        if (request.City is not null) user.City = request.City;
        if (request.State is not null) user.State = request.State;
        user.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.Users.UpdateAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(MapToDto(user));
    }

    public async Task<Result> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(id, cancellationToken);

        if (user is null)
        {
            return Result.Failure(DomainErrors.User.NotFound);
        }

        await _unitOfWork.Users.DeleteAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private static UserDto MapToDto(ApplicationUser user) => new(
        user.Id,
        user.PhoneNumber ?? "",
        user.Name,
        user.Email,
        user.Country,
        user.City,
        user.State,
        user.IsActive,
        user.CreatedAt
    );
}
