namespace HrSystemApp.Application.Interfaces.Services;

public interface ISmsService
{
    Task SendOtpAsync(string phoneNumber, string otp, CancellationToken cancellationToken = default);
}
