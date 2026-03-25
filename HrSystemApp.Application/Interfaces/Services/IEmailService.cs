namespace HrSystemApp.Application.Interfaces.Services;

public interface IEmailService
{
    Task SendOtpAsync(string toEmail, string otp, CancellationToken cancellationToken = default);
    Task SendWelcomeEmailAsync(string toEmail, string name, string companyName, string temporaryPassword, CancellationToken cancellationToken = default);
}
