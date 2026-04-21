using System.Diagnostics;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.Common.Logging;
using HrSystemApp.Application.DTOs.Auth;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HrSystemApp.Application.Features.Auth.Commands.LoginUser;

public class LoginUserCommandHandler : IRequestHandler<LoginUserCommand, Result<AuthResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITokenService _tokenService;
    private readonly ILogger<LoginUserCommandHandler> _logger;
    private readonly LoggingOptions _loggingOptions;

    public LoginUserCommandHandler(
        IUnitOfWork unitOfWork,
        ITokenService tokenService,
        ILogger<LoginUserCommandHandler> logger,
        IOptions<LoggingOptions> loggingOptions)
    {
        _unitOfWork = unitOfWork;
        _tokenService = tokenService;
        _logger = logger;
        _loggingOptions = loggingOptions.Value;
    }

    public async Task<Result<AuthResponse>> Handle(LoginUserCommand request, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        _logger.LogActionStart(_loggingOptions, LogAction.Auth.LoginUser);

        var user = await _unitOfWork.Users.GetByEmailWithDetailsAsync(request.Email, cancellationToken);

        if (user is null)
        {
            _logger.LogWarningUnauthorized(_loggingOptions, LogAction.Auth.LoginUser);
            sw.Stop();
            return Result.Failure<AuthResponse>(DomainErrors.Auth.InvalidCredentials);
        }

        var passwordValid = await _unitOfWork.Users.CheckPasswordAsync(user, request.Password);
        if (!passwordValid)
        {
            _logger.LogDecision(_loggingOptions, LogAction.Auth.LoginUser, LogStage.Authorization,
                "InvalidPassword", new { EmailDomain = user.Email?.Split('@').Last() });
            sw.Stop();
            return Result.Failure<AuthResponse>(DomainErrors.Auth.InvalidCredentials);
        }

        if (!user.IsActive)
        {
            _logger.LogDecision(_loggingOptions, LogAction.Auth.LoginUser, LogStage.Authorization,
                "AccountInactive", new { EmailDomain = user.Email?.Split('@').Last() });
            sw.Stop();
            return Result.Failure<AuthResponse>(DomainErrors.Auth.AccountInactive);
        }

        if (user.Employee?.Company != null && user.Employee.Company.Status != CompanyStatus.Active)
        {
            _logger.LogDecision(_loggingOptions, LogAction.Auth.LoginUser, LogStage.Authorization,
                "CompanyInactive", new { CompanyId = user.Employee.Company.Id });
            sw.Stop();
            return Result.Failure<AuthResponse>(DomainErrors.Auth.CompanyInactive);
        }

        if (user.Employee != null)
        {
            var blockedStatuses = new[]
            {
                EmploymentStatus.Terminated,
                EmploymentStatus.Inactive,
                EmploymentStatus.Suspended
            };

            if (blockedStatuses.Contains(user.Employee.EmploymentStatus))
            {
                _logger.LogDecision(_loggingOptions, LogAction.Auth.LoginUser, LogStage.Authorization,
                    "EmployeeBlockedStatus", new { EmployeeId = user.Employee.Id, Status = user.Employee.EmploymentStatus.ToString() });
                sw.Stop();
                return Result.Failure<AuthResponse>(DomainErrors.Auth.EmployeeBlockedStatus);
            }
        }

        var roles = await _unitOfWork.Users.GetRolesAsync(user);

        if (user.MustChangePassword)
        {
            _logger.LogDecision(_loggingOptions, LogAction.Auth.LoginUser, LogStage.Authorization,
                "MustChangePassword", new { UserId = user.Id });
            sw.Stop();
            _logger.LogActionSuccess(_loggingOptions, LogAction.Auth.LoginUser, sw.ElapsedMilliseconds);
            return Result.Success(new AuthResponse(
                Token: null,
                RefreshToken: null,
                UserId: user.Id,
                Email: user.Email!,
                Name: user.Name,
                Role: roles.FirstOrDefault() ?? string.Empty,
                EmployeeId: user.EmployeeId,
                MustChangePassword: true,
                ExpiresAt: null,
                PhoneNumber: user.PhoneNumber,
                Language: user.Language
            ));
        }

        var (token, expiresAt) = _tokenService.GenerateToken(user, roles);
        var refreshToken = _tokenService.GenerateRefreshToken();
        var refreshTokenHash = _tokenService.HashToken(refreshToken);

        await _unitOfWork.RefreshTokens.AddAsync(new HrSystemApp.Domain.Models.RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshTokenHash,
            ExpiresAt = DateTime.UtcNow.AddDays(_tokenService.RefreshTokenExpirationInDays),
            CreatedByIp = request.IpAddress
        }, cancellationToken);

        user.FcmToken = request.FcmToken ?? user.FcmToken;
        user.DeviceType = request.DeviceType ?? user.DeviceType;
        user.Language = request.Language ?? user.Language;
        user.LastLoginAt = DateTime.UtcNow;

        await _unitOfWork.Users.UpdateAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        sw.Stop();
        _logger.LogActionSuccess(_loggingOptions, LogAction.Auth.LoginUser, sw.ElapsedMilliseconds);

        return Result.Success(new AuthResponse(
            Token: token,
            RefreshToken: refreshToken,
            UserId: user.Id,
            Email: user.Email!,
            Name: user.Name,
            Role: roles.FirstOrDefault() ?? string.Empty,
            EmployeeId: user.EmployeeId,
            MustChangePassword: false,
            ExpiresAt: expiresAt,
            PhoneNumber: user.PhoneNumber,
            Language: user.Language
        ));
    }
}