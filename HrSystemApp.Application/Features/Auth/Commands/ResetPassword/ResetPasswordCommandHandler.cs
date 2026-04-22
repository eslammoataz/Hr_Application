using System.Diagnostics;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Auth;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Repositories;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Application.Common.Logging;

namespace HrSystemApp.Application.Features.Auth.Commands.ResetPassword;

public class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand, Result<AuthResponse>>
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITokenService _tokenService;
    private readonly ILogger<ResetPasswordCommandHandler> _logger;
    private readonly LoggingOptions _loggingOptions;

    public ResetPasswordCommandHandler(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        ITokenService tokenService,
        ILogger<ResetPasswordCommandHandler> logger,
        IOptions<LoggingOptions> loggingOptions)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _tokenService = tokenService;
        _logger = logger;
        _loggingOptions = loggingOptions.Value;
    }

    public async Task<Result<AuthResponse>> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        _logger.LogActionStart(_loggingOptions, LogAction.Auth.ResetPassword);

        const string otpPurpose = "PasswordReset";
        var otpProvider = TokenOptions.DefaultPhoneProvider;

        var user = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);

        if (user == null)
        {
            _logger.LogDecision(_loggingOptions, LogAction.Auth.ResetPassword, LogStage.Authorization,
                "UserNotFound", new { EmailDomain = request.Email.Split('@').Last() });
            sw.Stop();
            return Result.Failure<AuthResponse>(DomainErrors.User.NotFound);
        }

        if (user.OtpAttempts >= 3)
        {
            _logger.LogDecision(_loggingOptions, LogAction.Auth.ResetPassword, LogStage.Authorization,
                "MaxAttemptsReached", new { UserId = user.Id, Attempts = user.OtpAttempts });
            user.OtpAttempts = 0;
            await _userRepository.UpdateAsync(user, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            sw.Stop();
            return Result.Failure<AuthResponse>(DomainErrors.User.OtpMaxAttemptsReached);
        }

        var isValid = await _userRepository.VerifyUserTokenAsync(user, otpProvider, otpPurpose, request.Otp);

        if (!isValid)
        {
            user.OtpAttempts++;
            await _userRepository.UpdateAsync(user, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _logger.LogDecision(_loggingOptions, LogAction.Auth.ResetPassword, LogStage.Authorization,
                "InvalidOtp", new { UserId = user.Id, Attempts = user.OtpAttempts, Otp = request.Otp });
            sw.Stop();
            return Result.Failure<AuthResponse>(DomainErrors.User.InvalidOtp);
        }

        user.OtpAttempts = 0;
        user.MustChangePassword = false;

        var passwordChangeResult = await _userRepository.SetPasswordAsync(user, request.NewPassword);
        if (!passwordChangeResult.Succeeded)
        {
            var passwordErrors = passwordChangeResult.Errors.ToArray();
            _logger.LogDecision(_loggingOptions, LogAction.Auth.ResetPassword, LogStage.Processing,
                "PasswordChangeFailed", new { UserId = user.Id, ErrorCount = passwordErrors.Length });
            sw.Stop();
            return Result.Failure<AuthResponse>(new Error(DomainErrors.Auth.ResetFailed.Code,
                string.Join(", ", passwordErrors)));
        }

        user.SecurityStamp = Guid.NewGuid().ToString();
        await _userRepository.UpdateAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var roles = await _userRepository.GetRolesAsync(user);
        var (token, expiresAt) = _tokenService.GenerateToken(user, roles);

        sw.Stop();
        _logger.LogActionSuccess(_loggingOptions, LogAction.Auth.ResetPassword, sw.ElapsedMilliseconds);

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