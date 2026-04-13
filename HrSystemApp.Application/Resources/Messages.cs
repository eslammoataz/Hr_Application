namespace HrSystemApp.Application.Resources;

/// <summary>
/// Centralized application messages for validation and domain errors
/// </summary>
public static class Messages
{
    public static class Validation
    {
        // General
        public const string FieldRequired = "A required field is missing.";
        public const string InvalidType = "Field has an invalid data type.";
        public const string ValidationError = "An error occurred during validation.";

        // Auth
        public const string EmailRequired = "Email is required.";
        public const string ValidEmailRequired = "A valid email address is required.";
        public const string PasswordRequired = "Password is required.";
        public const string PasswordMinLength = "Password must be at least 6 characters.";
        public const string PasswordMinLength8 = "New password must be at least 8 characters.";
        public const string NewPasswordDifferent = "New password must be different from the current password.";
        public const string CurrentPasswordRequired = "Current password is required.";
        public const string FcmTokenNotEmpty = "FCM token cannot be empty if provided.";
        public const string InvalidDeviceType = "Invalid device type specified.";
        public const string LanguageNotEmpty = "Language cannot be empty if provided.";
        public const string LanguageMaxLength = "Language code must not exceed 10 characters.";
        public const string InvalidRole = "Invalid role specified.";
        public const string CannotAssignSuperAdmin = "Cannot assign SuperAdmin role via this endpoint.";
        public const string CannotAssignSuperAdminRegistration = "Cannot assign SuperAdmin role via registration endpoint.";
        public const string InvalidDeliveryChannel = "Invalid delivery channel specified.";

        // OTP
        public const string OtpRequired = "OTP is required.";
        public const string OtpMustBe6Chars = "OTP must be exactly 6 characters.";
        public const string OtpMustBeNumeric = "OTP must be numeric.";

        // Employee
        public const string FullNameRequired = "Full name is required.";
        public const string FullNameMaxLength = "Full name cannot exceed 200 characters.";
        public const string PhoneRequired = "Phone number is required.";
        public const string PhoneMustBeDigits = "Phone number must be 7-15 digits.";
        public const string CompanyIdRequired = "Company ID is required.";
        public const string TeamIdRequiredForTeamLeader = "Team ID is required for TeamLeader.";
        public const string UnitIdRequiredForUnitLeader = "Unit ID is required for UnitLeader.";
        public const string DepartmentIdRequired = "Department ID is required for this role.";

        // Name
        public const string NameRequired = "Name is required.";
        public const string NameMaxLength = "Name must not exceed 100 characters.";

        // Notification
        public const string EmployeeIdRequired = "EmployeeId is required.";
        public const string TitleRequired = "Title is required.";
        public const string TitleMaxLength = "Title must not exceed 200 characters.";
        public const string MessageRequired = "Message is required.";
        public const string MessageMaxLength = "Message must not exceed 2000 characters.";
        public const string InvalidNotificationType = "Invalid notification type.";

        // Department
        public const string CompanyIdRequiredForDepartment = "CompanyId is required.";
        public const string DepartmentNameRequired = "Department name is required and must not exceed 200 characters.";

        // Contact Admin
        public const string EmailNotValid = "Email is not valid.";
        public const string EmailMaxLength = "Email must not exceed 255 characters.";
        public const string CompanyNameRequired = "Company Name is required.";
        public const string CompanyNameMaxLength = "Company Name must not exceed 200 characters.";
        public const string PhoneNumberRequired = "Phone Number is required.";
        public const string PhoneNumberMaxLength = "Phone Number must not exceed 50 characters.";

        // Company Location
        public const string LocationNameRequired = "Location name is required.";
        public const string LocationNameMaxLength = "Location name cannot exceed 100 characters.";

        // Company
        public const string CompanyNameRequiredForCompany = "Company name is required.";
        public const string CompanyNameMaxLengthForCompany = "Company name cannot exceed 200 characters.";
        public const string YearlyVacationDaysRequired = "Yearly vacation days is required.";
        public const string GraceMinutesMustBeNonNegative = "Grace minutes must be zero or positive.";
        public const string TimeZoneIdRequired = "Time zone id is required.";
        public const string TimeZoneIdMaxLength = "Time zone id cannot exceed 100 characters.";
    }

    public static class Errors
    {
        // Auth Errors
        public const string InvalidCredentials = "Invalid email or password.";
        public const string UserNotFound = "User was not found.";
        public const string AccountInactive = "Your account is inactive. Please contact HR.";
        public const string CompanyInactive = "Your company's account is currently inactive. Please contact support.";
        public const string TokenNotFound = "Authentication token was not found.";
        public const string TokenExpired = "Authentication token has expired.";
        public const string Unauthorized = "You are not authorized to perform this action.";
        public const string EmployeeBlockedStatus = "Your account is not active. Please contact HR.";
        public const string InvalidRefreshToken = "The refresh token is invalid.";
        public const string RefreshTokenExpired = "The refresh token has expired.";
        public const string RefreshTokenRevoked = "The refresh token has been revoked.";
        public const string RefreshTokenReused = "Suspicious activity detected: Refresh token reuse. All sessions revoked.";
        public const string ResetFailed = "Failed to reset password.";
        public const string PasswordChangeFailed = "Failed to change password.";
        public const string ForcedChangeNotRequired = "User is not required to change their password via this endpoint.";

        // User Errors
        public const string UserNotFoundError = "User was not found.";
        public const string UserAlreadyExists = "User with this email already exists.";
        public const string UserUpdateFailed = "Failed to update user.";
        public const string UserDeleteFailed = "Failed to delete user.";
        public const string InvalidOtp = "Invalid OTP code provided.";
        public const string OtpMaxAttemptsReached = "Maximum OTP attempts reached. Please request a new code.";

        // Employee Errors
        public const string EmployeeNotFoundError = "Employee was not found.";
        public const string EmployeeAlreadyExists = "An employee with this email or phone number already exists.";
        public const string EmployeeCreationFailed = "Failed to create employee account. Please try again.";
        public const string EmployeeAlreadyInactive = "Employee is already inactive.";
        public const string InvalidEmploymentStatus = "Employment status value is invalid.";

        // Company
        public const string CompanyNotFoundError = "Company was not found.";

        // Department
        public const string DepartmentNotFoundError = "Department was not found.";
        public const string DepartmentAlreadyExists = "A department with this name already exists in the company.";

        // Unit
        public const string UnitNotFoundError = "Unit was not found.";
        public const string UnitAlreadyExists = "A unit with this name already exists in the department.";

        // Team
        public const string TeamNotFoundError = "Team was not found.";
        public const string TeamAlreadyExists = "A team with this name already exists in the unit.";

        // LeaveBalances
        public const string LeaveBalanceNotFound = "Leave balance record was not found for this employee and year.";
        public const string LeaveBalanceAlreadyInitialized = "Leave balance for this type and year already exists.";
        public const string InsufficientLeaveBalance = "Insufficient leave balance for the requested duration.";
        public const string InvalidDuration = "Duration must be positive.";

        // Requests
        public const string RequestNotFound = "Request was not found.";
        public const string RequestTypeDisabled = "The requested type is not available for your company.";
        public const string RequestNotPending = "Only pending requests can be modified or deleted.";
        public const string RequestModificationLocked = "Request is locked once approval process has started.";
        public const string RequestDefinitionNotFound = "Request configuration for this type was not found.";
        public const string RequestUnauthorized = "You are not the designated approver for this request at this stage.";
        public const string RequestLocked = "Request is not in a state that can be approved or handled.";

        // LeaveBalance (legacy)
        public const string LeaveBalanceNotFoundError = "No leave balance found for this employee and year.";
        public const string InsufficientLeaveBalanceError = "Insufficient leave balance.";

        // Notification
        public const string NotificationNotFound = "Notification was not found.";
        public const string NotificationForbidden = "You are not allowed to access this notification.";
        public const string NotificationSendFailed = "Failed to send the notification.";
        public const string NotificationTokenMissing = "The target employee does not have a valid FCM token.";

        // Attendance
        public const string AttendanceNotFound = "Attendance record was not found.";
        public const string AlreadyClockedIn = "Employee already has an open attendance record.";
        public const string ClockInRequired = "Employee must clock in before clocking out.";
        public const string AlreadyClockedOut = "Employee has already clocked out.";
        public const string InvalidClockOut = "Clock-out time must be after clock-in time.";
        public const string OverrideReasonRequired = "Override reason is required.";

        // Workflows
        public const string WorkflowNotFound = "No approval workflow defined for this request type.";
        public const string WorkflowInvalidStep = "The workflow step is no longer valid or has changed.";

        // Storage
        public const string StorageBucketNotFound = "The storage bucket was not found.";
        public const string StorageObjectNotFound = "The object was not found in storage.";
        public const string StorageUploadFailed = "Failed to upload the file to storage.";
        public const string StorageDeleteFailed = "Failed to delete the object from storage.";
        public const string StorageListFailed = "Failed to list objects in storage.";
        public const string StoragePresignedUrlFailed = "Failed to generate a download URL.";

        // General
        public const string ServerError = "An unexpected error occurred.";
        public const string ValidationErrorGeneral = "One or more validation errors occurred.";
        public const string ResourceNotFound = "The requested resource was not found.";
        public const string ArgumentError = "An invalid argument was provided.";
        public const string InvalidOperationError = "The requested operation is not valid in the current state.";
        public const string ForbiddenError = "You are not authorized to access this resource.";

        // ContactAdmin
        public const string ContactAdminNotFound = "Contact admin request was not found.";
        public const string ContactAdminAlreadyProcessed = "This request has already been processed.";
        public const string DuplicatePendingRequest = "A pending request with this email or company name already exists.";
        public const string PhoneNumberAlreadyTaken = "This phone number is already taken.";
        public const string EmailAlreadyTaken = "This email is already taken.";
        public const string ContactAdminCompanyNameTaken = "This company name is already taken.";

        // Hr
        public const string HrEmployeeNotFound = "The acting user has no employee record.";

        // ProfileUpdate
        public const string ProfileUpdateNotFound = "Request not found.";
        public const string ProfileUpdateNotPending = "Only pending requests can be handled.";
        public const string ProfileUpdateEmployeeNotFound = "Employee not found. Data may be corrupted.";
        public const string ProfileUpdateEmptyChanges = "ChangesJson is empty for approved request. Cannot apply changes.";
        public const string ProfileUpdateDeserializationFailed = "Failed to deserialize ChangesJson for request.";
        public const string ProfileUpdateUnknownField = "Unknown field in ChangesJson.";
        public const string ProfileUpdateHasPending = "You already have a pending profile update request.";
        public const string ProfileUpdateInvalidField = "Field is not allowed for update.";
        public const string ProfileUpdateNoChanges = "No new or valid changes provided. The entered values map to your existing profile.";
        public const string ProfileUpdateMalformedChanges = "One or more fields in the changes payload are missing the required 'newValue' key.";
        public const string ProfileUpdateInvalidLocationId = "Invalid CompanyLocationId provided.";

        // Hierarchy
        public const string HierarchyNotConfigured = "The company hierarchy has not been configured yet.";
        public const string HierarchyInvalidRole = "One or more roles are not valid for the company hierarchy. SuperAdmin is not allowed.";
        public const string HierarchyDuplicateRole = "Each role can only appear once in the hierarchy configuration.";
        public const string HierarchyMultipleCeos = "Only one CEO position can be configured per company.";
        public const string HierarchyWorkflowRoleNotInHierarchy = "One or more workflow step roles are not configured in the company hierarchy.";
        public const string HierarchyInvalidStepOrder = "Workflow steps must strictly escalate in authority order.";
        public const string HierarchyRoleInUse = "Cannot remove role because it is currently being used in one or more active Request Definitions.";
    }
}
