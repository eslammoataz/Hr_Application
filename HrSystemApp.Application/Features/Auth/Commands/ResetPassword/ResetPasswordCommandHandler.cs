using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Auth;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Repositories;
using HrSystemApp.Application.Interfaces.Services;
using System;
using System.Linq;

namespace HrSystemApp.Application.Features.Auth.Commands.ResetPassword;

public class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand, Result<AuthResponse>>
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITokenService _tokenService;
    private readonly ILogger<ResetPasswordCommandHandler> _logger;

    public ResetPasswordCommandHandler(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        ITokenService tokenService,
        ILogger<ResetPasswordCommandHandler> logger)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task<Result<AuthResponse>> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        const string otpPurpose = "PasswordReset";
        var otpProvider = TokenOptions.DefaultEmailProvider;

        var user = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);

        if (user == null)
            return Result.Failure<AuthResponse>(DomainErrors.User.NotFound);

        // Check if max attempts reached
        if (user.OtpAttempts >= 3)
        {
            user.OtpAttempts = 0; // Reset for next time if they request a new OTP
            await _userRepository.UpdateAsync(user, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Failure<AuthResponse>(DomainErrors.User.OtpMaxAttemptsReached);
        }

        _logger.LogInformation(
            "Reset password requested for user {UserId} using OTP provider {OtpProvider}.",
            user.Id,
            otpProvider);

        var isValid = await _userRepository.VerifyUserTokenAsync(user, otpProvider, otpPurpose, request.Otp);

        if (!isValid)
        {
            user.OtpAttempts++;
            await _userRepository.UpdateAsync(user, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _logger.LogWarning(
                "Invalid OTP for user {UserId}. Attempts: {OtpAttempts}. Provider: {OtpProvider}.",
                user.Id,
                user.OtpAttempts,
                otpProvider);
            return Result.Failure<AuthResponse>(DomainErrors.User.InvalidOtp);
        }

        // Valid OTP - reset attempts
        user.OtpAttempts = 0;

        // Generate Identity reset token
        var resetToken = await _userRepository.GeneratePasswordResetTokenAsync(user);

        // Perform reset
        var resetResult = await _userRepository.ResetPasswordAsync(user, resetToken, request.NewPassword);

        if (!resetResult.Succeeded)
        {
            var resetErrors = resetResult.Errors.ToArray();
            var hasInvalidTokenError = resetErrors.Any(e =>
                e.Contains("Invalid token", StringComparison.OrdinalIgnoreCase));

            _logger.LogWarning(
                "Password reset failed for user {UserId}. Errors: {ResetErrors}. PossibleConcurrentSubmission: {PossibleConcurrentSubmission}.",
                user.Id,
                string.Join(" | ", resetErrors),
                hasInvalidTokenError);

            return Result.Failure<AuthResponse>(new Error("Auth.ResetFailed",
                string.Join(", ", resetErrors)));
        }

        // Update user state if needed (e.g., MustChangePassword = false)
        user.MustChangePassword = false;
        await _userRepository.UpdateAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Successfully reset password for user: {Email}", request.Email);

        // Return AuthResponse so user is logged in immediately
        var roles = await _userRepository.GetRolesAsync(user);
        var (token, expiresAt) = _tokenService.GenerateToken(user, roles);

        return Result.Success(new AuthResponse(
            Token: token,
            UserId: user.Id,
            Email: user.Email!,
            Name: user.Name,
            Role: roles.FirstOrDefault() ?? string.Empty,
            EmployeeId: user.EmployeeId,
            MustChangePassword: user.MustChangePassword,
            ExpiresAt: expiresAt
        ));
    }
}
