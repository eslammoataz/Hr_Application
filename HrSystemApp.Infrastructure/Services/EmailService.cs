using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Application.Settings;

namespace HrSystemApp.Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly EmailSettings _settings;

    public EmailService(IOptions<EmailSettings> settings)
    {
        _settings = settings.Value;
    }

    public async Task SendOtpAsync(string toEmail, string otp, CancellationToken cancellationToken = default)
    {
        using var client = new SmtpClient(_settings.SmtpHost, _settings.SmtpPort)
        {
            Credentials = new NetworkCredential(_settings.SenderEmail, _settings.AppPassword),
            EnableSsl = true
        };

        using var mailMessage = new MailMessage
        {
            From = new MailAddress(_settings.SenderEmail, _settings.SenderName),
            Subject = "Your Password Reset OTP",
            Body = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #e0e0e0; border-radius: 10px;'>
                    <h2 style='color: #007bff; text-align: center;'>Password Reset Request</h2>
                    <p>Hello,</p>
                    <p>You requested a password reset for your HR System account. Please use the following One-Time Password (OTP) to complete the process:</p>
                    <div style='background-color: #f8f9fa; padding: 15px; text-align: center; border-radius: 5px; margin: 20px 0;'>
                        <span style='font-size: 32px; font-weight: bold; letter-spacing: 5px; color: #333;'>{otp}</span>
                    </div>
                    <p>This code is valid for a limited time. If you did not request this, please ignore this email.</p>
                    <hr style='border: 0; border-top: 1px solid #eeeeee; margin: 20px 0;'>
                    <p style='font-size: 12px; color: #777; text-align: center;'>&copy; {DateTime.UtcNow.Year} HR System. All rights reserved.</p>
                </div>",
            IsBodyHtml = true
        };
        mailMessage.To.Add(toEmail);

        await client.SendMailAsync(mailMessage);
    }
}
