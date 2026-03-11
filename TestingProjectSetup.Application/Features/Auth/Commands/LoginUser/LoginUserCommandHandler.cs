using MediatR;
using Microsoft.Extensions.Logging;
using TestingProjectSetup.Application.Common;
using TestingProjectSetup.Application.DTOs.Auth;
using TestingProjectSetup.Application.Errors;
using TestingProjectSetup.Application.Interfaces;
using TestingProjectSetup.Application.Interfaces.Services;

namespace TestingProjectSetup.Application.Features.Auth.Commands.LoginUser;

public class LoginUserCommandHandler : IRequestHandler<LoginUserCommand, Result<AuthResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITokenService _tokenService;
    private readonly ILogger<LoginUserCommandHandler> _logger;

    public LoginUserCommandHandler(
        IUnitOfWork unitOfWork,
        ITokenService tokenService,
        ILogger<LoginUserCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task<Result<AuthResponse>> Handle(LoginUserCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _unitOfWork.Users.GetByEmailAsync(request.Email, cancellationToken);

            if (user is null || !user.IsActive)
            {
                _logger.LogWarning("Invalid login attempt for email: {Email}", request.Email);
                return Result.Failure<AuthResponse>(DomainErrors.Auth.InvalidCredentials);
            }

            var passwordValid = await _unitOfWork.Users.CheckPasswordAsync(user, request.Password);
            if (!passwordValid)
            {
                _logger.LogWarning("Invalid password attempt for email: {Email}", request.Email);
                return Result.Failure<AuthResponse>(DomainErrors.Auth.InvalidCredentials);
            }

            var token = _tokenService.GenerateToken(user);
            // await _unitOfWork.Users.SaveTokenAsync(user.Id, token, cancellationToken);

            _logger.LogInformation("User {UserId} logged in successfully", user.Id);

            return Result.Success(new AuthResponse(
                token,
                user.Id,
                user.Email!,
                user.Name,
                DateTime.UtcNow.AddHours(24)
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login for {Email}", request.Email);
            return Result.Failure<AuthResponse>(DomainErrors.General.ServerError);
        }
    }
}
