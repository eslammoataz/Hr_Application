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
        var otpProvider = TokenOptions.DefaultPhoneProvider;

        _logger.LogInformation(
            "Reset password request received. Email: {Email}, Otp: {Otp}, Provider: {OtpProvider}, Purpose: {Purpose}.",
            request.Email,
            request.Otp,
            otpProvider,
            otpPurpose);

        var user = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);

        if (user == null)
            return Result.Failure<AuthResponse>(DomainErrors.User.NotFound);

        // Check if max attempts reached
        if (user.OtpAttempts >= 3)
        {
            _logger.LogWarning(
                "Reset password blocked due to max attempts. UserId: {UserId}, Attempts: {OtpAttempts}.",
                user.Id,
                user.OtpAttempts);
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
        _logger.LogInformation(
            "OTP validation result for user {UserId}: {IsValid}. Provider: {OtpProvider}, Purpose: {Purpose}.",
            user.Id,
            isValid,
            otpProvider,
            otpPurpose);

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

        // Valid OTP - manually reset password after OTP verification.
        user.OtpAttempts = 0;
        user.MustChangePassword = false;

        var passwordChangeResult = await _userRepository.SetPasswordAsync(user, request.NewPassword);
        if (!passwordChangeResult.Succeeded)
        {
            var passwordErrors = passwordChangeResult.Errors.ToArray();
            _logger.LogWarning(
                "Password change failed for user {UserId}. Errors: {PasswordErrors}.",
                user.Id,
                string.Join(" | ", passwordErrors));
            return Result.Failure<AuthResponse>(new Error("Auth.ResetFailed",
                string.Join(", ", passwordErrors)));
        }

        user.SecurityStamp = Guid.NewGuid().ToString();

        await _userRepository.UpdateAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Password reset applied manually for user {UserId}. Security stamp rotated.",
            user.Id);

        _logger.LogInformation("Successfully reset password for user: {Email}", request.Email);

        // Return AuthResponse so user is logged in immediately
        var roles = await _userRepository.GetRolesAsync(user);
        var (token, expiresAt) = _tokenService.GenerateToken(user, roles);

        return Result.Success(new AuthResponse(
            Token: token,
            RefreshToken: null,
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
