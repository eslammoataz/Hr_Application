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

        var user = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);

        if (user == null)
        {
            _logger.LogWarning("Forgot password requested for non-existent email: {Email}", request.Email);
            // Return success for security (prevent email enumeration)
            return Result.Success();
        }

        // Use an explicit provider so generation/verification stay deterministic across environments.
        var provider = TokenOptions.DefaultEmailProvider;


        var otp = await _userRepository.GenerateUserTokenAsync(user, provider, otpPurpose);

        // Reset attempts
        user.OtpAttempts = 0;
        await _userRepository.UpdateAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Generated OTP for user {Email}", request.Email);

        // Publish event for delivery
        await _publisher.Publish(new OtpGeneratedEvent(
            user.Email!,
            user.PhoneNumber,
            otp,
            request.Channel), cancellationToken);

        return Result.Success();
    }
}
