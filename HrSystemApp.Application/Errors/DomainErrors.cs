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

        public static readonly Error ResetFailed = new(
            "Auth.ResetFailed", "Failed to reset password.");

        public static readonly Error PasswordChangeFailed = new(
            "Auth.PasswordChangeFailed", "Failed to change password.");

        public static readonly Error ForcedChangeNotRequired = new(
            "Auth.ForcedChangeNotRequired", "User is not required to change their password via this endpoint.");
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

        public static readonly Error InvalidEmploymentStatus = new(
            "Employee.InvalidEmploymentStatus", "Employment status value is invalid.");
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

    public static class LeaveBalances
    {
        public static readonly Error NotFound = new(
            "LeaveBalance.NotFound", "Leave balance record was not found for this employee and year.");

        public static readonly Error AlreadyInitialized = new(
            "LeaveBalance.AlreadyInitialized", "Leave balance for this type and year already exists.");

        public static readonly Error Insufficient = new(
            "LeaveBalance.Insufficient", "Insufficient leave balance for the requested duration.");
            
        public static readonly Error InvalidDuration = new(
            "LeaveBalance.InvalidDuration", "Duration must be positive.");
    }

    public static class Requests
    {
        public static readonly Error NotFound = new(
            "Request.NotFound", "Request was not found.");

        public static readonly Error TypeDisabled = new(
            "Request.TypeDisabled", "The requested type is not available for your company.");

        public static readonly Error NotPending = new(
            "Request.NotPending", "Only pending requests can be modified or deleted.");

        public static readonly Error ModificationLocked = new(
            "Request.ModificationLocked", "Request is locked once approval process has started.");
            
        public static readonly Error DefinitionNotFound = new(
            "Request.DefinitionNotFound", "Request configuration for this type was not found.");

        public static readonly Error Unauthorized = new(
            "Request.Unauthorized", "You are not the designated approver for this request at this stage.");

        public static readonly Error Locked = new(
            "Request.Locked", "Request is not in a state that can be approved or handled.");

        public static readonly Error InvalidDuration = new(
            "Request.InvalidDuration", "Duration must be positive.");
    }

    public static class LeaveBalance
    {
        public static readonly Error NotFound = new(
            "LeaveBalance.NotFound", "No leave balance found for this employee and year.");

        public static readonly Error Insufficient = new(
            "LeaveBalance.Insufficient", "Insufficient leave balance.");
    }

    public static class Notification
    {
        public static readonly Error NotFound = new(
            "Notification.NotFound", "Notification was not found.");

        public static readonly Error Forbidden = new(
            "Notification.Forbidden", "You are not allowed to access this notification.");

        public static readonly Error SendFailed = new(
            "Notification.SendFailed", "Failed to send the notification.");

        public static readonly Error TokenMissing = new(
            "Notification.TokenMissing", "The target employee does not have a valid FCM token.");
    }

    public static class Attendance
    {
        public static readonly Error NotFound = new(
            "Attendance.NotFound", "Attendance record was not found.");

        public static readonly Error AlreadyClockedIn = new(
            "Attendance.AlreadyClockedIn", "Employee already has an open attendance record.");

        public static readonly Error ClockInRequired = new(
            "Attendance.ClockInRequired", "Employee must clock in before clocking out.");

        public static readonly Error AlreadyClockedOut = new(
            "Attendance.AlreadyClockedOut", "Employee has already clocked out.");

        public static readonly Error InvalidClockOut = new(
            "Attendance.InvalidClockOut", "Clock-out time must be after clock-in time.");

        public static readonly Error OverrideReasonRequired = new(
            "Attendance.OverrideReasonRequired", "Override reason is required.");
    }

    public static class Workflows
    {
        public static readonly Error NotFound = new(
            "Workflow.NotFound", "No approval workflow defined for this request type.");

        public static readonly Error InvalidStep = new(
            "Workflow.InvalidStep", "The workflow step is no longer valid or has changed.");
    }

    public static class Storage
    {
        public static readonly Error BucketNotFound = new(
            "Storage.BucketNotFound", "The storage bucket was not found.");

        public static readonly Error ObjectNotFound = new(
            "Storage.ObjectNotFound", "The object was not found in storage.");

        public static readonly Error UploadFailed = new(
            "Storage.UploadFailed", "Failed to upload the file to storage.");

        public static readonly Error DeleteFailed = new(
            "Storage.DeleteFailed", "Failed to delete the object from storage.");

        public static readonly Error ListFailed = new(
            "Storage.ListFailed", "Failed to list objects in storage.");

        public static readonly Error PresignedUrlFailed = new(
            "Storage.PresignedUrlFailed", "Failed to generate a download URL.");
    }

    public static class General
    {
        public static readonly Error ServerError = new(
            "General.ServerError", "An unexpected error occurred.");

        public static readonly Error ValidationError = new(
            "General.ValidationError", "One or more validation errors occurred.");

        public static readonly Error NotFound = new(
            "General.NotFound", "The requested resource was not found.");

        public static readonly Error ArgumentError = new(
            "General.ArgumentError", "An invalid argument was provided.");

        public static readonly Error InvalidOperation = new(
            "General.InvalidOperation", "The requested operation is not valid in the current state.");

        public static readonly Error Forbidden = new(
            "General.Forbidden", "You are not authorized to access this resource.");
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

    public static class Hierarchy
    {
        public static readonly Error NotConfigured = new(
            "Hierarchy.NotConfigured", "The company hierarchy has not been configured yet.");

        public static readonly Error InvalidRole = new(
            "Hierarchy.InvalidRole", "One or more roles are not valid for the company hierarchy. SuperAdmin is not allowed.");

        public static readonly Error DuplicateRole = new(
            "Hierarchy.DuplicateRole", "Each role can only appear once in the hierarchy configuration.");

        public static readonly Error MultipleCeos = new(
            "Hierarchy.MultipleCeos", "Only one CEO position can be configured per company.");

        public static readonly Error WorkflowRoleNotInHierarchy = new(
            "Hierarchy.WorkflowRoleNotInHierarchy", "One or more workflow step roles are not configured in the company hierarchy.");

        public static readonly Error InvalidStepOrder = new(
            "Hierarchy.InvalidStepOrder", "Workflow steps must strictly escalate in authority order.");

        public static readonly Error RoleInUse = new(
            "Hierarchy.RoleInUse", "Cannot remove role because it is currently being used in one or more active Request Definitions.");
    }

    public static class Validation
    {
        public static readonly Error FieldRequired = new(
            "Validation.FieldRequired", "A required field is missing.");

        public static readonly Error InvalidType = new(
            "Validation.InvalidType", "Field has an invalid data type.");

        public static readonly Error Error = new(
            "Validation.Error", "An error occurred during validation.");
    }
}

