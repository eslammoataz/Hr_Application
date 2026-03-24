using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces.Repositories;

namespace HrSystemApp.Application.Features.Auth.Commands.VerifyOtp;

public class VerifyOtpCommandHandler : IRequestHandler<VerifyOtpCommand, Result<bool>>
{
    private readonly IUserRepository _userRepository;
    private readonly ILogger<VerifyOtpCommandHandler> _logger;

    public VerifyOtpCommandHandler(
        IUserRepository userRepository,
        ILogger<VerifyOtpCommandHandler> logger)
    {
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<Result<bool>> Handle(VerifyOtpCommand request, CancellationToken cancellationToken)
    {
        const string otpPurpose = "PasswordReset";
        var otpProvider = TokenOptions.DefaultPhoneProvider;

        var user = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (user == null)
        {
            _logger.LogWarning("OTP verification requested for unknown email: {Email}", request.Email);
            return Result.Failure<bool>(DomainErrors.User.NotFound);
        }

        var isValid = await _userRepository.VerifyUserTokenAsync(user, otpProvider, otpPurpose, request.Otp);
        if (!isValid)
        {
            _logger.LogWarning("OTP verification failed for user {UserId}.", user.Id);
            return Result.Failure<bool>(DomainErrors.User.InvalidOtp);
        }

        _logger.LogInformation("OTP verified successfully for user {UserId}.", user.Id);
        return Result.Success(true);
    }
}
