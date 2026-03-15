using MediatR;
using Microsoft.Extensions.Logging;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Auth;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Models;

namespace HrSystemApp.Application.Features.Auth.Commands.RegisterUser;

public class RegisterUserCommandHandler : IRequestHandler<RegisterUserCommand, Result<AuthResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITokenService _tokenService;
    private readonly ILogger<RegisterUserCommandHandler> _logger;

    public RegisterUserCommandHandler(
        IUnitOfWork unitOfWork,
        ITokenService tokenService,
        ILogger<RegisterUserCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task<Result<AuthResponse>> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var existingUser = await _unitOfWork.Users.GetByEmailAsync(request.Email, cancellationToken);
            if (existingUser != null)
                return Result.Failure<AuthResponse>(DomainErrors.User.AlreadyExists);

            var user = new ApplicationUser
            {
                Email = request.Email,
                UserName = request.Email,
                Name = request.Name,
                PhoneNumber = request.PhoneNumber,
                EmailConfirmed = true,
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
                Role = request.Role
            };

            var success = await _unitOfWork.Users.CreateUserAsync(user, request.Password, request.Role, cancellationToken);

            if (!success)
                return Result.Failure<AuthResponse>(DomainErrors.General.ServerError);

            var (token, expiresAt) = _tokenService.GenerateToken(user);
            await _unitOfWork.Users.SaveTokenAsync(user.Id, token, cancellationToken);

            _logger.LogInformation("User {UserId} registered successfully", user.Id);

            return Result.Success(new AuthResponse(
                token,
                user.Id,
                user.Email!,
                user.Name,
                user.Role.ToString(),
                user.EmployeeId,
                user.MustChangePassword,
                expiresAt
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during user registration for {Email}", request.Email);
            return Result.Failure<AuthResponse>(DomainErrors.General.ServerError);
        }
    }
}
