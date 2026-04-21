using System.Diagnostics;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Common.Events;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Repositories;
using HrSystemApp.Domain.Models;
using HrSystemApp.Application.Common.Logging;

namespace HrSystemApp.Application.Features.Auth.Commands.ForgotPassword;

public class ForgotPasswordCommandHandler : IRequestHandler<ForgotPasswordCommand, Result>
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPublisher _publisher;
    private readonly ILogger<ForgotPasswordCommandHandler> _logger;
    private readonly LoggingOptions _loggingOptions;

    public ForgotPasswordCommandHandler(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IPublisher publisher,
        ILogger<ForgotPasswordCommandHandler> logger,
        IOptions<LoggingOptions> loggingOptions)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _publisher = publisher;
        _logger = logger;
        _loggingOptions = loggingOptions.Value;
    }

    public async Task<Result> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        _logger.LogActionStart(_loggingOptions, LogAction.Auth.ForgotPassword);

        var emailDomain = request.Email.Split('@').Last();

        var user = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);

        if (user == null)
        {
            _logger.LogDecision(_loggingOptions, LogAction.Auth.ForgotPassword, LogStage.Authorization,
                "UserNotFound", new { EmailDomain = emailDomain });
            sw.Stop();
            return Result.Failure(DomainErrors.Auth.UserNotFound);
        }

        const string otpPurpose = "PasswordReset";
        var otpProvider = TokenOptions.DefaultPhoneProvider;

        var otp = await _userRepository.GenerateUserTokenAsync(user, otpProvider, otpPurpose);
        user.OtpAttempts = 0;
        await _userRepository.UpdateAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _publisher.Publish(new OtpGeneratedEvent(
            user.Email!,
            user.PhoneNumber,
            otp,
            request.Channel), cancellationToken);

        sw.Stop();
        _logger.LogActionSuccess(_loggingOptions, LogAction.Auth.ForgotPassword, sw.ElapsedMilliseconds);

        return Result.Success();
    }
}