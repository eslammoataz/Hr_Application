using System.Diagnostics;
using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Application.Settings;
using HrSystemApp.Application.Common.Logging;

namespace HrSystemApp.Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly EmailSettings _settings;
    private readonly ILogger<EmailService> _logger;
    private readonly LoggingOptions _loggingOptions;

    public EmailService(
        IOptions<EmailSettings> settings,
        ILogger<EmailService> logger,
        IOptions<LoggingOptions> loggingOptions)
    {
        _settings = settings.Value;
        _logger = logger;
        _loggingOptions = loggingOptions.Value;
    }

    public async Task SendOtpAsync(string toEmail, string otp, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var emailDomain = MaskEmail(toEmail);

        _logger.LogExternalCall(_loggingOptions, LogAction.Attendance.SendEmail, "SmtpSendOtp", 0);

        try
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

            await client.SendMailAsync(mailMessage, cancellationToken);

            sw.Stop();
            _logger.LogExternalCall(_loggingOptions, LogAction.Attendance.ClockIn, "SmtpSendOtp", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogActionFailure(_loggingOptions, LogAction.Attendance.SendEmail, LogStage.ExternalCall, ex,
                new { EmailDomain = emailDomain, TemplateId = "OTP" });
            throw;
        }
    }

    public async Task SendWelcomeEmailAsync(string toEmail, string name, string companyName, string temporaryPassword, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var emailDomain = MaskEmail(toEmail);

        _logger.LogExternalCall(_loggingOptions, LogAction.Attendance.SendEmail, "SmtpSendWelcome", 0);

        try
        {
            using var client = new SmtpClient(_settings.SmtpHost, _settings.SmtpPort)
            {
                Credentials = new NetworkCredential(_settings.SenderEmail, _settings.AppPassword),
                EnableSsl = true
            };

            using var mailMessage = new MailMessage
            {
                From = new MailAddress(_settings.SenderEmail, _settings.SenderName),
                Subject = "Welcome to HR System",
                Body = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #e0e0e0; border-radius: 10px;'>
                    <h2 style='color: #28a745; text-align: center;'>Welcome, {name}!</h2>
                    <p>Your request to join the HR System for <strong>{companyName}</strong> has been accepted.</p>
                    <p>We are excited to have you on board! Use the following credentials to log in to your company admin account:</p>
                    <ul style='background-color: #f8f9fa; padding: 15px 30px; border-radius: 5px; margin: 20px 0;'>
                        <li><strong>Email (Login ID):</strong> {toEmail}</li>
                        <li><strong>Temporary Password:</strong> {temporaryPassword}</li>
                    </ul>
                    <p style='color: #dc3545; font-weight: bold;'>Please note: You will be required to change your password upon your first login for security reasons.</p>
                    <hr style='border: 0; border-top: 1px solid #eeeeee; margin: 20px 0;'>
                    <p style='font-size: 12px; color: #777; text-align: center;'>&copy; {DateTime.UtcNow.Year} HR System. All rights reserved.</p>
                </div>",
                IsBodyHtml = true
            };
            mailMessage.To.Add(toEmail);

            await client.SendMailAsync(mailMessage, cancellationToken);

            sw.Stop();
            _logger.LogExternalCall(_loggingOptions, LogAction.Attendance.ClockIn, "SmtpSendWelcome", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogActionFailure(_loggingOptions, LogAction.Attendance.SendEmail, LogStage.ExternalCall, ex,
                new { EmailDomain = emailDomain, TemplateId = "Welcome", CompanyName = companyName });
            throw;
        }
    }

    private static string MaskEmail(string email)
    {
        var parts = email.Split('@');
        return parts.Length == 2 ? $"***@{parts[1]}" : "***";
    }
}