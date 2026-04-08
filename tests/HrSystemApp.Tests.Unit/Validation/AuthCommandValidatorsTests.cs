using FluentAssertions;
using HrSystemApp.Application.Features.Auth.Commands.ChangePassword;
using HrSystemApp.Application.Features.Auth.Commands.ForceChangePassword;
using HrSystemApp.Application.Features.Auth.Commands.ForgotPassword;
using HrSystemApp.Application.Features.Auth.Commands.LoginUser;
using HrSystemApp.Application.Features.Auth.Commands.RegisterUser;
using HrSystemApp.Application.Features.Auth.Commands.ResetPassword;
using HrSystemApp.Application.Features.Auth.Commands.UpdateFcmToken;
using HrSystemApp.Application.Features.Auth.Commands.UpdateLanguage;
using HrSystemApp.Application.Features.Auth.Commands.VerifyOtp;
using HrSystemApp.Domain.Enums;

namespace HrSystemApp.Tests.Unit.Validation;

public class AuthCommandValidatorsTests
{
    [Fact]
    public void ChangePassword_NewPasswordSameAsCurrent_ShouldFail()
    {
        var validator = new ChangePasswordCommandValidator();
        var command = new ChangePasswordCommand("user-1", "Password1", "Password1");

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void ForceChangePassword_EmptyUserId_ShouldFail()
    {
        var validator = new ForceChangePasswordCommandValidator();
        var command = new ForceChangePasswordCommand("", "Old12345", "New12345");

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.PropertyName == "UserId");
    }

    [Fact]
    public void ForgotPassword_InvalidEmail_ShouldFail()
    {
        var validator = new ForgotPasswordCommandValidator();
        var command = new ForgotPasswordCommand("not-an-email", OtpChannel.Email);

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void LoginUser_LanguageTooLong_ShouldFail()
    {
        var validator = new LoginUserCommandValidator();
        var command = new LoginUserCommand("valid@email.com", "pass", null, null, "abcdefghijklmnop");

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.PropertyName == "Language");
    }

    [Fact]
    public void RegisterUser_SuperAdminRole_ShouldFail()
    {
        var validator = new RegisterUserCommandValidator();
        var command = new RegisterUserCommand("Name", "n@a.com", "0100000000", "pass123", UserRole.SuperAdmin);

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.PropertyName == "Role");
    }

    [Fact]
    public void ResetPassword_NonNumericOtp_ShouldFail()
    {
        var validator = new ResetPasswordCommandValidator();
        var command = new ResetPasswordCommand("valid@email.com", "ABC123", "newpassword");

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.PropertyName == "Otp");
    }

    [Fact]
    public void UpdateFcmToken_EmptyToken_ShouldFail()
    {
        var validator = new UpdateFcmTokenCommandValidator();
        var command = new UpdateFcmTokenCommand("user-1", "", DeviceType.Android);

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.PropertyName == "FcmToken");
    }

    [Fact]
    public void UpdateLanguage_EmptyLanguage_ShouldFail()
    {
        var validator = new UpdateLanguageCommandValidator();
        var command = new UpdateLanguageCommand("user-1", "");

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.PropertyName == "Language");
    }

    [Fact]
    public void VerifyOtp_InvalidLength_ShouldFail()
    {
        var validator = new VerifyOtpCommandValidator();
        var command = new VerifyOtpCommand("valid@email.com", "12345");

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.PropertyName == "Otp");
    }
}
