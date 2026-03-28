using HrSystemApp.Application.Common;

namespace HrSystemApp.Application.Errors;

/// <summary>
/// Domain-specific errors
/// </summary>
public static class DomainErrors
{
    public static class Auth
    {
        public static readonly Error InvalidCredentials = new(
            "Auth.InvalidCredentials", "Invalid email or password.");

        public static readonly Error UserNotFound = new(
            "Auth.UserNotFound", "User was not found.");

        public static readonly Error AccountInactive = new(
            "Auth.AccountInactive", "Your account is inactive. Please contact HR.");

        public static readonly Error CompanyInactive = new(
            "Auth.CompanyInactive", "Your company's account is currently inactive. Please contact support.");

        public static readonly Error TokenNotFound = new(
            "Auth.TokenNotFound", "Authentication token was not found.");

        public static readonly Error TokenExpired = new(
            "Auth.TokenExpired", "Authentication token has expired.");

        public static readonly Error Unauthorized = new(
            "Auth.Unauthorized", "You are not authorized to perform this action.");

        public static readonly Error EmployeeBlockedStatus = new(
            "Auth.EmployeeBlockedStatus",
            "Your account is not active. Please contact HR.");

        public static readonly Error InvalidRefreshToken = new(
            "Auth.InvalidRefreshToken", "The refresh token is invalid.");

        public static readonly Error RefreshTokenExpired = new(
            "Auth.RefreshTokenExpired", "The refresh token has expired.");

        public static readonly Error RefreshTokenRevoked = new(
            "Auth.RefreshTokenRevoked", "The refresh token has been revoked.");
            
        public static readonly Error RefreshTokenReused = new(
            "Auth.RefreshTokenReused", "Suspicious activity detected: Refresh token reuse. All sessions revoked.");
    }

    public static class User
    {
        public static readonly Error NotFound = new(
            "User.NotFound", "User was not found.");

        public static readonly Error AlreadyExists = new(
            "User.AlreadyExists", "User with this email already exists.");

        public static readonly Error UpdateFailed = new(
            "User.UpdateFailed", "Failed to update user.");

        public static readonly Error DeleteFailed = new(
            "User.DeleteFailed", "Failed to delete user.");

        public static readonly Error InvalidOtp = new(
            "User.InvalidOtp", "Invalid OTP code provided.");

        public static readonly Error OtpMaxAttemptsReached = new(
            "User.OtpMaxAttemptsReached", "Maximum OTP attempts reached. Please request a new code.");
    }

    public static class Employee
    {
        public static readonly Error NotFound = new(
            "Employee.NotFound", "Employee was not found.");

        public static readonly Error AlreadyExists = new(
            "Employee.AlreadyExists", "An employee with this email or phone number already exists.");

        public static readonly Error CreationFailed = new(
            "Employee.CreationFailed", "Failed to create employee account. Please try again.");

        public static readonly Error AlreadyInactive = new(
            "Employee.AlreadyInactive", "Employee is already inactive.");
    }

    public static class Company
    {
        public static readonly Error NotFound = new(
            "Company.NotFound", "Company was not found.");
    }

    public static class Department
    {
        public static readonly Error NotFound = new(
            "Department.NotFound", "Department was not found.");

        public static readonly Error AlreadyExists = new(
            "Department.AlreadyExists", "A department with this name already exists in the company.");
    }

    public static class Unit
    {
        public static readonly Error NotFound = new(
            "Unit.NotFound", "Unit was not found.");

        public static readonly Error AlreadyExists = new(
            "Unit.AlreadyExists", "A unit with this name already exists in the department.");
    }

    public static class Team
    {
        public static readonly Error NotFound = new(
            "Team.NotFound", "Team was not found.");

        public static readonly Error AlreadyExists = new(
            "Team.AlreadyExists", "A team with this name already exists in the unit.");
    }

    public static class LeaveBalance
    {
        public static readonly Error NotFound = new(
            "LeaveBalance.NotFound", "Leave balance record was not found.");

        public static readonly Error AlreadyInitialized = new(
            "LeaveBalance.AlreadyInitialized", "Leave balance for this type and year already exists.");
    }

    public static class General
    {
        public static readonly Error ServerError = new(
            "General.ServerError", "An unexpected error occurred.");

        public static readonly Error ValidationError = new(
            "General.ValidationError", "One or more validation errors occurred.");

        public static readonly Error NotFound = new(
            "General.NotFound", "The requested resource was not found.");
    }

    public static class ContactAdmin
    {
        public static readonly Error NotFound = new(
            "ContactAdmin.NotFound", "Contact admin request was not found.");

        public static readonly Error AlreadyProcessed = new(
            "ContactAdmin.AlreadyProcessed", "This request has already been processed.");

        public static readonly Error DuplicatePendingRequest = new(
            "ContactAdmin.DuplicatePendingRequest",
            "A pending request with this email or company name already exists.");

        public static readonly Error PhoneNumberAlreadyTaken = new(
            "ContactAdmin.PhoneNumberAlreadyTaken", "This phone number is already taken.");

        public static readonly Error EmailAlreadyTaken = new(
            "ContactAdmin.EmailAlreadyTaken", "This email is already taken.");

        public static readonly Error CompanyNameAlreadyTaken = new(
            "ContactAdmin.CompanyNameAlreadyTaken", "This company name is already taken.");
    }

    public static class Hr
    {
        public static readonly Error EmployeeNotFound = new(
            "Hr.EmployeeNotFound", "The acting user has no employee record.");
    }

    public static class ProfileUpdate
    {
        public static readonly Error NotFound = new(
            "ProfileUpdate.NotFound", "Request not found.");

        public static readonly Error NotPending = new(
            "ProfileUpdate.NotPending", "Only pending requests can be handled.");

        public static readonly Error EmployeeNotFound = new(
            "ProfileUpdate.EmployeeNotFound", "Employee not found. Data may be corrupted.");

        public static readonly Error EmptyChanges = new(
            "ProfileUpdate.EmptyChanges", "ChangesJson is empty for approved request. Cannot apply changes.");

        public static readonly Error DeserializationFailed = new(
            "ProfileUpdate.DeserializationFailed", "Failed to deserialize ChangesJson for request.");

        public static readonly Error UnknownField = new(
            "ProfileUpdate.UnknownField", "Unknown field in ChangesJson.");

        public static readonly Error HasPending = new(
            "ProfileUpdate.HasPending", "You already have a pending profile update request.");

        public static readonly Error InvalidField = new(
            "ProfileUpdate.InvalidField", "Field is not allowed for update.");

        public static readonly Error NoChanges = new(
            "ProfileUpdate.NoChanges",
            "No new or valid changes provided. The entered values map to your existing profile.");

        public static readonly Error MalformedChanges = new(
            "ProfileUpdate.MalformedChanges",
            "One or more fields in the changes payload are missing the required 'newValue' key.");

        public static readonly Error InvalidLocationId = new(
            "ProfileUpdate.InvalidLocationId",
            "Invalid CompanyLocationId provided.");
    }
}

