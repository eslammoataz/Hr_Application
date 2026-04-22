using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Auth;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Models;
using HrSystemApp.Application.Common.Logging;

namespace HrSystemApp.Application.Features.Auth.Commands.RegisterUser;

public class RegisterUserCommandHandler : IRequestHandler<RegisterUserCommand, Result<AuthResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITokenService _tokenService;
    private readonly ILogger<RegisterUserCommandHandler> _logger;
    private readonly LoggingOptions _loggingOptions;

    public RegisterUserCommandHandler(
        IUnitOfWork unitOfWork,
        ITokenService tokenService,
        ILogger<RegisterUserCommandHandler> logger,
        IOptions<LoggingOptions> loggingOptions)
    {
        _unitOfWork = unitOfWork;
        _tokenService = tokenService;
        _logger = logger;
        _loggingOptions = loggingOptions.Value;
    }

    public async Task<Result<AuthResponse>> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        _logger.LogActionStart(_loggingOptions, LogAction.Auth.RegisterUser);

        var existingUser = await _unitOfWork.Users.GetByEmailAsync(request.Email, cancellationToken);
        if (existingUser != null)
        {
            _logger.LogDecision(_loggingOptions, LogAction.Auth.RegisterUser, LogStage.Validation,
                "UserAlreadyExists", new { EmailDomain = request.Email.Split('@').Last() });
            sw.Stop();
            return Result.Failure<AuthResponse>(DomainErrors.User.AlreadyExists);
        }

        var user = new ApplicationUser
        {
            Email = request.Email,
            UserName = request.Email,
            Name = request.Name,
            PhoneNumber = request.PhoneNumber,
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        var success = await _unitOfWork.Users.CreateUserAsync(user, request.Password, request.Role, cancellationToken);

        if (!success)
        {
            _logger.LogDecision(_loggingOptions, LogAction.Auth.RegisterUser, LogStage.Processing,
                "UserCreationFailed", new { EmailDomain = request.Email.Split('@').Last() });
            sw.Stop();
            return Result.Failure<AuthResponse>(DomainErrors.General.ServerError);
        }

        var roles = await _unitOfWork.Users.GetRolesAsync(user);
        var (token, expiresAt) = _tokenService.GenerateToken(user, roles);
        await _unitOfWork.Users.SaveTokenAsync(user.Id, token, cancellationToken);

        sw.Stop();
        _logger.LogActionSuccess(_loggingOptions, LogAction.Auth.RegisterUser, sw.ElapsedMilliseconds);

        return Result.Success(new AuthResponse(
            Token: token,
            RefreshToken: null,
            UserId: user.Id,
            Email: user.Email!,
            Name: user.Name,
            Role: roles.FirstOrDefault() ?? string.Empty,
            EmployeeId: user.EmployeeId,
            MustChangePassword: user.MustChangePassword,
            ExpiresAt: expiresAt,
            PhoneNumber: user.PhoneNumber,
            Language: user.Language
        ));
    }
}