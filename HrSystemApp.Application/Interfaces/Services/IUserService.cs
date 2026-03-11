using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.User;

namespace HrSystemApp.Application.Interfaces.Services;

/// <summary>
/// User service interface
/// </summary>
public interface IUserService
{
    Task<Result<UserDto>> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<Result<UserDto>> GetByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken = default);
    Task<Result<PagedResult<UserDto>>> GetAllAsync(PaginationParams pagination, CancellationToken cancellationToken = default);
    Task<Result<UserDto>> UpdateAsync(string id, UpdateUserRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(string id, CancellationToken cancellationToken = default);
}
