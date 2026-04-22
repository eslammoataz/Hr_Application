namespace HrSystemApp.Application.Resources;

/// <summary>
/// Validation error codes used as lookup keys in ValidationMessages.resx.
/// Kept separate from Messages to allow code changes without touching resource files.
/// </summary>
public static class ErrorCodes
{
    // General
    public const string FieldRequired = "Val.FieldRequired";
    public const string InvalidType = "Val.InvalidType";
    public const string ValidationError = "Val.ValidationError";

    // Auth
    public const string EmailRequired = "Val.EmailRequired";
    public const string ValidEmailRequired = "Val.ValidEmailRequired";
    public const string PasswordRequired = "Val.PasswordRequired";
    public const string PasswordMinLength = "Val.PasswordMinLength";
    public const string PasswordMinLength8 = "Val.PasswordMinLength8";
    public const string NewPasswordDifferent = "Val.NewPasswordDifferent";
    public const string CurrentPasswordRequired = "Val.CurrentPasswordRequired";
    public const string FcmTokenNotEmpty = "Val.FcmTokenNotEmpty";
    public const string InvalidDeviceType = "Val.InvalidDeviceType";
    public const string LanguageNotEmpty = "Val.LanguageNotEmpty";
    public const string LanguageMaxLength = "Val.LanguageMaxLength";
    public const string InvalidRole = "Val.InvalidRole";
    public const string CannotAssignSuperAdmin = "Val.CannotAssignSuperAdmin";
    public const string CannotAssignSuperAdminRegistration = "Val.CannotAssignSuperAdminRegistration";
    public const string InvalidDeliveryChannel = "Val.InvalidDeliveryChannel";

    // OTP
    public const string OtpRequired = "Val.OtpRequired";
    public const string OtpMustBe6Chars = "Val.OtpMustBe6Chars";
    public const string OtpMustBeNumeric = "Val.OtpMustBeNumeric";

    // Employee
    public const string FullNameRequired = "Val.FullNameRequired";
    public const string FullNameMaxLength = "Val.FullNameMaxLength";
    public const string PhoneRequired = "Val.PhoneRequired";
    public const string PhoneMustBeDigits = "Val.PhoneMustBeDigits";
    public const string CompanyIdRequired = "Val.CompanyIdRequired";
    public const string TeamIdRequiredForTeamLeader = "Val.TeamIdRequiredForTeamLeader";
    public const string UnitIdRequiredForUnitLeader = "Val.UnitIdRequiredForUnitLeader";
    public const string DepartmentIdRequired = "Val.DepartmentIdRequired";

    // Name
    public const string NameRequired = "Val.NameRequired";
    public const string NameMaxLength = "Val.NameMaxLength";
    public const string OrgNodeNameMaxLength = "Val.OrgNodeNameMaxLength";

    // OrgNode / BulkSetup
    public const string BulkSetupRequestCannotBeNull = "Val.BulkSetupRequestCannotBeNull";
    public const string BulkSetupAtLeastOneNode = "Val.BulkSetupAtLeastOneNode";
    public const string BulkSetupTempIdsUnique = "Val.BulkSetupTempIdsUnique";
    public const string BulkSetupParentTempIdInvalid = "Val.BulkSetupParentTempIdInvalid";

    // Assign Employee
    public const string AssignEmployeeAlreadyAssigned = "Val.AssignEmployeeAlreadyAssigned";

    // Notification
    public const string EmployeeIdRequired = "Val.EmployeeIdRequired";
    public const string TitleRequired = "Val.TitleRequired";
    public const string TitleMaxLength = "Val.TitleMaxLength";
    public const string MessageRequired = "Val.MessageRequired";
    public const string MessageMaxLength = "Val.MessageMaxLength";
    public const string InvalidNotificationType = "Val.InvalidNotificationType";

    // Department
    public const string CompanyIdRequiredForDepartment = "Val.CompanyIdRequiredForDepartment";
    public const string DepartmentNameRequired = "Val.DepartmentNameRequired";

    // Contact Admin
    public const string EmailNotValid = "Val.EmailNotValid";
    public const string EmailMaxLength = "Val.EmailMaxLength";
    public const string CompanyNameRequired = "Val.CompanyNameRequired";
    public const string CompanyNameMaxLength = "Val.CompanyNameMaxLength";
    public const string PhoneNumberRequired = "Val.PhoneNumberRequired";
    public const string PhoneNumberMaxLength = "Val.PhoneNumberMaxLength";

    // Company Location
    public const string LocationNameRequired = "Val.LocationNameRequired";
    public const string LocationNameMaxLength = "Val.LocationNameMaxLength";

    // Company
    public const string CompanyNameRequiredForCompany = "Val.CompanyNameRequiredForCompany";
    public const string CompanyNameMaxLengthForCompany = "Val.CompanyNameMaxLengthForCompany";
    public const string YearlyVacationDaysRequired = "Val.YearlyVacationDaysRequired";
    public const string GraceMinutesMustBeNonNegative = "Val.GraceMinutesMustBeNonNegative";
    public const string TimeZoneIdRequired = "Val.TimeZoneIdRequired";
    public const string TimeZoneIdMaxLength = "Val.TimeZoneIdMaxLength";

    // Employee Update
    public const string EmployeeFullNameMaxLength = "Val.EmployeeFullNameMaxLength";
    public const string EmployeePhoneMaxLength = "Val.EmployeePhoneMaxLength";
    public const string EmployeeAddressMaxLength = "Val.EmployeeAddressMaxLength";

    // Attendance
    public const string ClockInFutureTimestamp = "Val.ClockInFutureTimestamp";
    public const string ClockOutFutureTimestamp = "Val.ClockOutFutureTimestamp";

    // Change Employee Status
    public const string ChangeEmployeeStatusIdRequired = "Val.ChangeEmployeeStatusIdRequired";
    public const string ChangeEmployeeStatusInvalid = "Val.ChangeEmployeeStatusInvalid";

    // Update Company
    public const string UpdateCompanyIdRequired = "Val.UpdateCompanyIdRequired";
    public const string UpdateCompanyNameRequired = "Val.UpdateCompanyNameRequired";
    public const string UpdateCompanyNameMaxLength = "Val.UpdateCompanyNameMaxLength";
    public const string UpdateCompanyGraceMinutesNegative = "Val.UpdateCompanyGraceMinutesNegative";
    public const string UpdateCompanyGraceMinutesExceed = "Val.UpdateCompanyGraceMinutesExceed";
    public const string UpdateCompanyVacationDaysPositive = "Val.UpdateCompanyVacationDaysPositive";
    public const string UpdateCompanyVacationDaysExceed = "Val.UpdateCompanyVacationDaysExceed";
    public const string UpdateCompanyTimeZoneRequired = "Val.UpdateCompanyTimeZoneRequired";
    public const string UpdateCompanyTimeZoneMaxLength = "Val.UpdateCompanyTimeZoneMaxLength";
    public const string UpdateCompanyStartBeforeEnd = "Val.UpdateCompanyStartBeforeEnd";

    // Update My Company
    public const string UpdateMyCompanyNameRequired = "Val.UpdateMyCompanyNameRequired";
    public const string UpdateMyCompanyNameMaxLength = "Val.UpdateMyCompanyNameMaxLength";
    public const string UpdateMyCompanyGraceMinutesNegative = "Val.UpdateMyCompanyGraceMinutesNegative";
    public const string UpdateMyCompanyGraceMinutesExceed = "Val.UpdateMyCompanyGraceMinutesExceed";
    public const string UpdateMyCompanyVacationDaysPositive = "Val.UpdateMyCompanyVacationDaysPositive";
    public const string UpdateMyCompanyVacationDaysExceed = "Val.UpdateMyCompanyVacationDaysExceed";
    public const string UpdateMyCompanyTimeZoneRequired = "Val.UpdateMyCompanyTimeZoneRequired";
    public const string UpdateMyCompanyTimeZoneMaxLength = "Val.UpdateMyCompanyTimeZoneMaxLength";
    public const string UpdateMyCompanyStartBeforeEnd = "Val.UpdateMyCompanyStartBeforeEnd";
}
