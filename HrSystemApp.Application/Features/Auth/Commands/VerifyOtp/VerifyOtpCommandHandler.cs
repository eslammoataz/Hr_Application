using System.Diagnostics;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces.Repositories;
using HrSystemApp.Application.Common.Logging;

namespace HrSystemApp.Application.Features.Auth.Commands.VerifyOtp;

public class VerifyOtpCommandHandler : IRequestHandler<VerifyOtpCommand, Result<bool>>
{
    private readonly IUserRepository _userRepository;
    private readonly ILogger<VerifyOtpCommandHandler> _logger;
    private readonly LoggingOptions _loggingOptions;

    public VerifyOtpCommandHandler(
        IUserRepository userRepository,
        ILogger<VerifyOtpCommandHandler> logger,
        IOptions<LoggingOptions> loggingOptions)
    {
        _userRepository = userRepository;
        _logger = logger;
        _loggingOptions = loggingOptions.Value;
    }

    public async Task<Result<bool>> Handle(VerifyOtpCommand request, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        _logger.LogActionStart(_loggingOptions, LogAction.Auth.VerifyOtp);

        var user = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (user == null)
        {
            _logger.LogDecision(_loggingOptions, LogAction.Auth.VerifyOtp, LogStage.Authorization,
                "UserNotFound", new { EmailDomain = request.Email.Split('@').Last() });
            sw.Stop();
            return Result.Failure<bool>(DomainErrors.User.NotFound);
        }

        var otpProvider = TokenOptions.DefaultPhoneProvider;
        var isValid = await _userRepository.VerifyUserTokenAsync(user, otpProvider, "PasswordReset", request.Otp);

        if (!isValid)
        {
            _logger.LogDecision(_loggingOptions, LogAction.Auth.VerifyOtp, LogStage.Authorization,
                "InvalidOtp", new { UserId = user.Id });
            sw.Stop();
            return Result.Failure<bool>(DomainErrors.User.InvalidOtp);
        }

        sw.Stop();
        _logger.LogActionSuccess(_loggingOptions, LogAction.Auth.VerifyOtp, sw.ElapsedMilliseconds);

        return Result.Success(true);
    }
}