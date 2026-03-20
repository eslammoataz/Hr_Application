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

        public static readonly Error TokenNotFound = new(
            "Auth.TokenNotFound", "Authentication token was not found.");

        public static readonly Error TokenExpired = new(
            "Auth.TokenExpired", "Authentication token has expired.");

        public static readonly Error Unauthorized = new(
            "Auth.Unauthorized", "You are not authorized to perform this action.");
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
}

