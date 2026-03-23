using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using HrSystemApp.Application.Common;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Application.Common.Events;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Repositories;
using HrSystemApp.Domain.Models;

namespace HrSystemApp.Application.Features.Auth.Commands.ForgotPassword;

public class ForgotPasswordCommandHandler : IRequestHandler<ForgotPasswordCommand, Result>
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPublisher _publisher;
    private readonly ILogger<ForgotPasswordCommandHandler> _logger;

    public ForgotPasswordCommandHandler(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IPublisher publisher,
        ILogger<ForgotPasswordCommandHandler> logger)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task<Result> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        const string otpPurpose = "PasswordReset";
        var provider = TokenOptions.DefaultPhoneProvider;

        _logger.LogInformation(
            "Forgot password requested. Email: {Email}, Channel: {Channel}, Provider: {Provider}, Purpose: {Purpose}.",
            request.Email,
            request.Channel,
            provider,
            otpPurpose);

        var user = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);

        if (user == null)
        {
            _logger.LogWarning("Forgot password requested for non-existent email: {Email}", request.Email);
            // Return success for security (prevent email enumeration)
            return Result.Success();
        }

        var otp = await _userRepository.GenerateUserTokenAsync(user, provider, otpPurpose);
        // Reset attempts
        user.OtpAttempts = 0;
        await _userRepository.UpdateAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Generated OTP for user {Email}. UserId: {UserId}, Otp: {Otp}, Provider: {Provider}, Purpose: {Purpose}.",
            request.Email,
            user.Id,
            otp,
            provider,
            otpPurpose);

        // Publish event for delivery
        await _publisher.Publish(new OtpGeneratedEvent(
            user.Email!,
            user.PhoneNumber,
            otp,
            request.Channel), cancellationToken);

        _logger.LogInformation(
            "OTP publish completed for user {Email}. UserId: {UserId}, Channel: {Channel}.",
            request.Email,
            user.Id,
            request.Channel);

        return Result.Success();
    }
}
