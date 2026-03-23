using Microsoft.Extensions.Logging;
using HrSystemApp.Application.Interfaces.Services;

namespace HrSystemApp.Infrastructure.Services;

public class SmsService : ISmsService
{
    private readonly ILogger<SmsService> _logger;

    public SmsService(ILogger<SmsService> logger)
    {
        _logger = logger;
    }

    public Task SendOtpAsync(string phoneNumber, string otp, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("SMS delivery is not implemented yet. OTP for {PhoneNumber} is: {Otp}", phoneNumber, otp);
        return Task.CompletedTask;
    }
}
