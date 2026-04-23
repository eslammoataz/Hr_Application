using HrSystemApp.Application.Common;
using HrSystemApp.Application.Resources;

namespace HrSystemApp.Application.Errors;

/// <summary>
/// Domain-specific errors
/// </summary>
public static class DomainErrors
{
    public static class Auth
    {
        public static readonly Error InvalidCredentials = new(
            "Auth.InvalidCredentials", Messages.Errors.InvalidCredentials);

        public static readonly Error UserNotFound = new(
            "Auth.UserNotFound", Messages.Errors.UserNotFound);

        public static readonly Error AccountInactive = new(
            "Auth.AccountInactive", Messages.Errors.AccountInactive);

        public static readonly Error CompanyInactive = new(
            "Auth.CompanyInactive", Messages.Errors.CompanyInactive);

        public static readonly Error TokenNotFound = new(
            "Auth.TokenNotFound", Messages.Errors.TokenNotFound);

        public static readonly Error TokenExpired = new(
            "Auth.TokenExpired", Messages.Errors.TokenExpired);

        public static readonly Error Unauthorized = new(
            "Auth.Unauthorized", Messages.Errors.Unauthorized);

        public static readonly Error EmployeeBlockedStatus = new(
            "Auth.EmployeeBlockedStatus",
            Messages.Errors.EmployeeBlockedStatus);

        public static readonly Error InvalidRefreshToken = new(
            "Auth.InvalidRefreshToken", Messages.Errors.InvalidRefreshToken);

        public static readonly Error RefreshTokenExpired = new(
            "Auth.RefreshTokenExpired", Messages.Errors.RefreshTokenExpired);

        public static readonly Error RefreshTokenRevoked = new(
            "Auth.RefreshTokenRevoked", Messages.Errors.RefreshTokenRevoked);

        public static readonly Error RefreshTokenReused = new(
            "Auth.RefreshTokenReused", Messages.Errors.RefreshTokenReused);

        public static readonly Error ResetFailed = new(
            "Auth.ResetFailed", Messages.Errors.ResetFailed);

        public static readonly Error PasswordChangeFailed = new(
            "Auth.PasswordChangeFailed", Messages.Errors.PasswordChangeFailed);

        public static readonly Error ForcedChangeNotRequired = new(
            "Auth.ForcedChangeNotRequired", Messages.Errors.ForcedChangeNotRequired);
    }

    public static class User
    {
        public static readonly Error NotFound = new(
            "User.NotFound", Messages.Errors.UserNotFoundError);

        public static readonly Error AlreadyExists = new(
            "User.AlreadyExists", Messages.Errors.UserAlreadyExists);

        public static readonly Error UpdateFailed = new(
            "User.UpdateFailed", Messages.Errors.UserUpdateFailed);

        public static readonly Error DeleteFailed = new(
            "User.DeleteFailed", Messages.Errors.UserDeleteFailed);

        public static readonly Error InvalidOtp = new(
            "User.InvalidOtp", Messages.Errors.InvalidOtp);

        public static readonly Error OtpMaxAttemptsReached = new(
            "User.OtpMaxAttemptsReached", Messages.Errors.OtpMaxAttemptsReached);
    }

    public static class Employee
    {
        public static readonly Error NotFound = new(
            "Employee.NotFound", Messages.Errors.EmployeeNotFoundError);

        public static readonly Error AlreadyExists = new(
            "Employee.AlreadyExists", Messages.Errors.EmployeeAlreadyExists);

        public static readonly Error CreationFailed = new(
            "Employee.CreationFailed", Messages.Errors.EmployeeCreationFailed);

        public static readonly Error AlreadyInactive = new(
            "Employee.AlreadyInactive", Messages.Errors.EmployeeAlreadyInactive);

        public static readonly Error InvalidEmploymentStatus = new(
            "Employee.InvalidEmploymentStatus", Messages.Errors.InvalidEmploymentStatus);
    }

    public static class Company
    {
        public static readonly Error NotFound = new(
            "Company.NotFound", Messages.Errors.CompanyNotFoundError);
    }

    public static class Department
    {
        public static readonly Error NotFound = new(
            "Department.NotFound", Messages.Errors.DepartmentNotFoundError);

        public static readonly Error AlreadyExists = new(
            "Department.AlreadyExists", Messages.Errors.DepartmentAlreadyExists);
    }

    public static class Unit
    {
        public static readonly Error NotFound = new(
            "Unit.NotFound", Messages.Errors.UnitNotFoundError);

        public static readonly Error AlreadyExists = new(
            "Unit.AlreadyExists", Messages.Errors.UnitAlreadyExists);
    }

    public static class Team
    {
        public static readonly Error NotFound = new(
            "Team.NotFound", Messages.Errors.TeamNotFoundError);

        public static readonly Error AlreadyExists = new(
            "Team.AlreadyExists", Messages.Errors.TeamAlreadyExists);
    }

    public static class LeaveBalances
    {
        public static readonly Error NotFound = new(
            "LeaveBalance.NotFound", Messages.Errors.LeaveBalanceNotFound);

        public static readonly Error AlreadyInitialized = new(
            "LeaveBalance.AlreadyInitialized", Messages.Errors.LeaveBalanceAlreadyInitialized);

        public static readonly Error Insufficient = new(
            "LeaveBalance.Insufficient", Messages.Errors.InsufficientLeaveBalance);

        public static readonly Error InvalidDuration = new(
            "LeaveBalance.InvalidDuration", Messages.Errors.InvalidDuration);
    }

    public static class Requests
    {
        public static readonly Error NotFound = new(
            "Request.NotFound", Messages.Errors.RequestNotFound);

        public static readonly Error TypeDisabled = new(
            "Request.TypeDisabled", Messages.Errors.RequestTypeDisabled);

        public static readonly Error NotPending = new(
            "Request.NotPending", Messages.Errors.RequestNotPending);

        public static readonly Error ModificationLocked = new(
            "Request.ModificationLocked", Messages.Errors.RequestModificationLocked);

        public static readonly Error DefinitionNotFound = new(
            "Request.DefinitionNotFound", Messages.Errors.RequestDefinitionNotFound);

        public static readonly Error DefinitionAlreadyExists = new(
            "Request.DefinitionAlreadyExists", "A request definition for this type already exists for this company.");

        public static readonly Error Unauthorized = new(
            "Request.Unauthorized", Messages.Errors.RequestUnauthorized);

        public static readonly Error Locked = new(
            "Request.Locked", Messages.Errors.RequestLocked);

        public static readonly Error InvalidDuration = new(
            "Request.InvalidDuration", Messages.Errors.InvalidDuration);

        public static readonly Error InvalidStatusForOperation = new(
            "Request.InvalidStatusForOperation", "The request status does not allow this operation.");
    }

    public static class LeaveBalance
    {
        public static readonly Error NotFound = new(
            "LeaveBalance.NotFound", Messages.Errors.LeaveBalanceNotFoundError);

        public static readonly Error Insufficient = new(
            "LeaveBalance.Insufficient", Messages.Errors.InsufficientLeaveBalanceError);
    }

    public static class Notification
    {
        public static readonly Error NotFound = new(
            "Notification.NotFound", Messages.Errors.NotificationNotFound);

        public static readonly Error Forbidden = new(
            "Notification.Forbidden", Messages.Errors.NotificationForbidden);

        public static readonly Error SendFailed = new(
            "Notification.SendFailed", Messages.Errors.NotificationSendFailed);

        public static readonly Error TokenMissing = new(
            "Notification.TokenMissing", Messages.Errors.NotificationTokenMissing);
    }

    public static class Attendance
    {
        public static readonly Error NotFound = new(
            "Attendance.NotFound", Messages.Errors.AttendanceNotFound);

        public static readonly Error AlreadyClockedIn = new(
            "Attendance.AlreadyClockedIn", Messages.Errors.AlreadyClockedIn);

        public static readonly Error ClockInRequired = new(
            "Attendance.ClockInRequired", Messages.Errors.ClockInRequired);

        public static readonly Error AlreadyClockedOut = new(
            "Attendance.AlreadyClockedOut", Messages.Errors.AlreadyClockedOut);

        public static readonly Error InvalidClockOut = new(
            "Attendance.InvalidClockOut", Messages.Errors.InvalidClockOut);

        public static readonly Error OverrideReasonRequired = new(
            "Attendance.OverrideReasonRequired", Messages.Errors.OverrideReasonRequired);
    }

    public static class Workflows
    {
        public static readonly Error NotFound = new(
            "Workflow.NotFound", Messages.Errors.WorkflowNotFound);

        public static readonly Error InvalidStep = new(
            "Workflow.InvalidStep", Messages.Errors.WorkflowInvalidStep);
    }

    public static class Storage
    {
        public static readonly Error BucketNotFound = new(
            "Storage.BucketNotFound", Messages.Errors.StorageBucketNotFound);

        public static readonly Error ObjectNotFound = new(
            "Storage.ObjectNotFound", Messages.Errors.StorageObjectNotFound);

        public static readonly Error UploadFailed = new(
            "Storage.UploadFailed", Messages.Errors.StorageUploadFailed);

        public static readonly Error DeleteFailed = new(
            "Storage.DeleteFailed", Messages.Errors.StorageDeleteFailed);

        public static readonly Error ListFailed = new(
            "Storage.ListFailed", Messages.Errors.StorageListFailed);

        public static readonly Error PresignedUrlFailed = new(
            "Storage.PresignedUrlFailed", Messages.Errors.StoragePresignedUrlFailed);
    }

    public static class General
    {
        public static readonly Error ServerError = new(
            "General.ServerError", Messages.Errors.ServerError);

        public static readonly Error ValidationError = new(
            "General.ValidationError", Messages.Errors.ValidationErrorGeneral);

        public static readonly Error NotFound = new(
            "General.NotFound", Messages.Errors.ResourceNotFound);

        public static readonly Error ArgumentError = new(
            "General.ArgumentError", Messages.Errors.ArgumentError);

        public static readonly Error InvalidOperation = new(
            "General.InvalidOperation", Messages.Errors.InvalidOperationError);

        public static readonly Error Forbidden = new(
            "General.Forbidden", Messages.Errors.ForbiddenError);
    }

    public static class ContactAdmin
    {
        public static readonly Error NotFound = new(
            "ContactAdmin.NotFound", Messages.Errors.ContactAdminNotFound);

        public static readonly Error AlreadyProcessed = new(
            "ContactAdmin.AlreadyProcessed", Messages.Errors.ContactAdminAlreadyProcessed);

        public static readonly Error DuplicatePendingRequest = new(
            "ContactAdmin.DuplicatePendingRequest",
            Messages.Errors.DuplicatePendingRequest);

        public static readonly Error PhoneNumberAlreadyTaken = new(
            "ContactAdmin.PhoneNumberAlreadyTaken", Messages.Errors.PhoneNumberAlreadyTaken);

        public static readonly Error EmailAlreadyTaken = new(
            "ContactAdmin.EmailAlreadyTaken", Messages.Errors.EmailAlreadyTaken);

        public static readonly Error CompanyNameAlreadyTaken = new(
            "ContactAdmin.CompanyNameAlreadyTaken", Messages.Errors.ContactAdminCompanyNameTaken);
    }

    public static class Hr
    {
        public static readonly Error EmployeeNotFound = new(
            "Hr.EmployeeNotFound", Messages.Errors.HrEmployeeNotFound);
    }

    public static class ProfileUpdate
    {
        public static readonly Error NotFound = new(
            "ProfileUpdate.NotFound", Messages.Errors.ProfileUpdateNotFound);

        public static readonly Error NotPending = new(
            "ProfileUpdate.NotPending", Messages.Errors.ProfileUpdateNotPending);

        public static readonly Error EmployeeNotFound = new(
            "ProfileUpdate.EmployeeNotFound", Messages.Errors.ProfileUpdateEmployeeNotFound);

        public static readonly Error EmptyChanges = new(
            "ProfileUpdate.EmptyChanges", Messages.Errors.ProfileUpdateEmptyChanges);

        public static readonly Error DeserializationFailed = new(
            "ProfileUpdate.DeserializationFailed", Messages.Errors.ProfileUpdateDeserializationFailed);

        public static readonly Error UnknownField = new(
            "ProfileUpdate.UnknownField", Messages.Errors.ProfileUpdateUnknownField);

        public static readonly Error HasPending = new(
            "ProfileUpdate.HasPending", Messages.Errors.ProfileUpdateHasPending);

        public static readonly Error InvalidField = new(
            "ProfileUpdate.InvalidField", Messages.Errors.ProfileUpdateInvalidField);

        public static readonly Error NoChanges = new(
            "ProfileUpdate.NoChanges",
            Messages.Errors.ProfileUpdateNoChanges);

        public static readonly Error MalformedChanges = new(
            "ProfileUpdate.MalformedChanges",
            Messages.Errors.ProfileUpdateMalformedChanges);

        public static readonly Error InvalidLocationId = new(
            "ProfileUpdate.InvalidLocationId",
            Messages.Errors.ProfileUpdateInvalidLocationId);
    }

    public static class Hierarchy
    {
        public static readonly Error NotConfigured = new(
            "Hierarchy.NotConfigured", Messages.Errors.HierarchyNotConfigured);

        public static readonly Error InvalidRole = new(
            "Hierarchy.InvalidRole", Messages.Errors.HierarchyInvalidRole);

        public static readonly Error DuplicateRole = new(
            "Hierarchy.DuplicateRole", Messages.Errors.HierarchyDuplicateRole);

        public static readonly Error MultipleCeos = new(
            "Hierarchy.MultipleCeos", Messages.Errors.HierarchyMultipleCeos);

        public static readonly Error WorkflowRoleNotInHierarchy = new(
            "Hierarchy.WorkflowRoleNotInHierarchy", Messages.Errors.HierarchyWorkflowRoleNotInHierarchy);

        public static readonly Error InvalidStepOrder = new(
            "Hierarchy.InvalidStepOrder", Messages.Errors.HierarchyInvalidStepOrder);

        public static readonly Error RoleInUse = new(
            "Hierarchy.RoleInUse", Messages.Errors.HierarchyRoleInUse);
    }

    public static class Validation
    {
        public static readonly Error FieldRequired = new(
            "Validation.FieldRequired", Messages.Validation.FieldRequired);

        public static readonly Error InvalidType = new(
            "Validation.InvalidType", Messages.Validation.InvalidType);

        public static readonly Error Error = new(
            "Validation.Error", Messages.Validation.ValidationError);
    }

    public static class OrgNode
    {
        public static readonly Error NotFound = new(
            "OrgNode.NotFound", "The requested organization node was not found.");

        public static readonly Error CircularReference = new(
            "OrgNode.CircularReference", "Cannot create a circular hierarchy reference.");

        public static readonly Error DuplicateAssignment = new(
            "OrgNode.DuplicateAssignment", "This employee is already assigned to this node.");

        public static readonly Error AssignmentNotFound = new(
            "OrgNode.AssignmentNotFound", "Assignment not found.");

        public static readonly Error InvalidHierarchyConfiguration = new(
            "OrgNode.InvalidHierarchyConfiguration", "Invalid hierarchy configuration detected.");
    }

    public static class Roles
    {
        public static readonly Error NotFound =
            new("Roles.NotFound", "The role was not found.");

        public static readonly Error NameAlreadyExists =
            new("Roles.NameAlreadyExists", "A role with this name already exists in the company.");

        public static readonly Error InUseByWorkflow =
            new("Roles.InUseByWorkflow", "This role is referenced by an active workflow definition and cannot be deleted.");

        public static readonly Error AlreadyAssigned =
            new("Roles.AlreadyAssigned", "This role is already assigned to the employee.");

        public static readonly Error AssignmentNotFound =
            new("Roles.AssignmentNotFound", "This role assignment does not exist.");

        public static readonly Error InvalidPermission =
            new("Roles.InvalidPermission", "One or more permissions are not valid. Check AppPermissions for allowed values.");
    }

    public static class Request
    {
        public static readonly Error NoActiveManagersAtStep = new(
            "Request.NoActiveManagersAtStep", "A workflow step has no active managers assigned.");

        public static readonly Error InvalidWorkflowChain = new(
            "Request.InvalidWorkflowChain", "Workflow step references a node not in the approval chain.");

        public static readonly Error NotPendingApproval = new(
            "Request.NotPendingApproval", "This request is not currently awaiting approval.");

        public static readonly Error StepOrderExceeded = new(
            "Request.StepOrderExceeded", "Step order exceeds the number of workflow steps.");

        public static readonly Error OrgNodeNotInCompany = new(
            "Request.OrgNodeNotInCompany", "The referenced org node does not belong to this company.");

        public static readonly Error DirectEmployeeNotInCompany = new(
            "Request.DirectEmployeeNotInCompany", "The referenced employee does not belong to this company.");

        public static readonly Error DirectEmployeeNotActive = new(
            "Request.DirectEmployeeNotActive", "The referenced employee is not active.");

        public static readonly Error MissingDirectEmployeeId = new(
            "Request.MissingDirectEmployeeId", "A DirectEmployee step must specify a DirectEmployeeId.");

        public static readonly Error MissingOrgNodeId = new(
            "Request.MissingOrgNodeId", "An OrgNode step must specify an OrgNodeId.");

        public static readonly Error MissingCompanyRoleId = new(
            "Request.MissingCompanyRoleId", "A CompanyRole step must specify a CompanyRoleId.");

        public static readonly Error RoleNotInCompany = new(
            "Request.RoleNotInCompany", "The referenced company role does not belong to this company.");

        public static readonly Error RoleNotFound = new(
            "Request.RoleNotFound", "The referenced company role was not found.");

        public static readonly Error MissingLevelsUp = new(
            "Request.MissingLevelsUp", "A HierarchyLevel step must specify LevelsUp >= 1.");

        public static readonly Error InvalidStartFromLevel = new(
            "Request.InvalidStartFromLevel", "StartFromLevel must be >= 1.");

        public static readonly Error UnexpectedFieldsOnHierarchyLevelStep = new(
            "Request.UnexpectedFieldsOnHierarchyLevelStep", "HierarchyLevel steps must not have OrgNodeId, DirectEmployeeId, or BypassHierarchyCheck.");

        public static readonly Error HierarchyLevelFieldsOnNonHierarchyStep = new(
            "Request.HierarchyLevelFieldsOnNonHierarchyStep", "StartFromLevel and LevelsUp are only valid on HierarchyLevel steps.");

        public static readonly Error HierarchyRangesOverlap = new(
            "Request.HierarchyRangesOverlap", "HierarchyLevel step ranges must not overlap.");
    }
}
