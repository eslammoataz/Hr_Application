namespace HrSystemApp.Application.Common.Logging;

public static class LogAction
{
    public static class Workflow
    {
        public const string ApproveRequest              = "ApproveRequest";
        public const string CreateRequest               = "CreateRequest";
        public const string RejectRequest               = "RejectRequest";
        public const string UpdateRequest               = "UpdateRequest";
        public const string DeleteRequest               = "DeleteRequest";
        public const string CancelRequest               = "CancelRequest";
        public const string CreateRequestDefinition     = "CreateRequestDefinition";
        public const string UpdateRequestDefinition     = "UpdateRequestDefinition";
        public const string DeleteRequestDefinition     = "DeleteRequestDefinition";
        public const string HandleProfileUpdateRequest  = "HandleProfileUpdateRequest";
        public const string InitializeYearlyBalances   = "InitializeYearlyBalances";
        public const string UpdateEmployeeBalance       = "UpdateEmployeeBalance";
        public const string LeaveValidation            = "LeaveValidation";
        public const string PreviewApprovalChain       = "PreviewApprovalChain";
    }

    public static class Auth
    {
        public const string LoginUser             = "LoginUser";
        public const string RegisterUser          = "RegisterUser";
        public const string ChangePassword        = "ChangePassword";
        public const string ForceChangePassword   = "ForceChangePassword";
        public const string ForgotPassword        = "ForgotPassword";
        public const string ResetPassword         = "ResetPassword";
        public const string VerifyOtp             = "VerifyOtp";
        public const string RefreshToken          = "RefreshToken";
        public const string LogoutUser            = "LogoutUser";
        public const string RevokeToken           = "RevokeToken";
        public const string RevokeAllTokens       = "RevokeAllTokens";
        public const string UpdateFcmToken        = "UpdateFcmToken";
        public const string UpdateLanguage        = "UpdateLanguage";
    }

    public static class OrgNode
    {
        public const string CreateOrgNode            = "CreateOrgNode";
        public const string UpdateOrgNode            = "UpdateOrgNode";
        public const string DeleteOrgNode            = "DeleteOrgNode";
        public const string AssignEmployeeToNode     = "AssignEmployeeToNode";
        public const string UnassignEmployeeFromNode = "UnassignEmployeeFromNode";
        public const string BulkSetupOrgNodes        = "BulkSetupOrgNodes";
        public const string CreateEmployee           = "CreateEmployee";
        public const string UpdateEmployee           = "UpdateEmployee";
        public const string ChangeEmployeeStatus     = "ChangeEmployeeStatus";
        public const string GetOrgNodeTree           = "GetOrgNodeTree";
        public const string GetOrgNodeDetails        = "GetOrgNodeDetails";
        public const string GetMyCompanyHierarchy   = "GetMyCompanyHierarchy";
    }

    public static class Attendance
    {
        public const string ClockIn            = "ClockIn";
        public const string ClockOut           = "ClockOut";
        public const string OverrideClockIn    = "OverrideClockIn";
        public const string OverrideClockOut   = "OverrideClockOut";
        public const string AttendanceReminder = "AttendanceReminder";
        public const string AutoClockOut       = "AutoClockOut";
        public const string SendEmail          = "SendEmail";
        public const string SendSms            = "SendSms";
    }

    public static class Notifications
    {
        public const string SendToEmployee = "SendToEmployee";
    }
}
