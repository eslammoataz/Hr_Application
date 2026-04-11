using FluentValidation;
using HrSystemApp.Domain.Enums;

namespace HrSystemApp.Application.Features.Employees.Commands.CreateEmployee;

public class CreateEmployeeCommandValidator : AbstractValidator<CreateEmployeeCommand>
{
    public CreateEmployeeCommandValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Full name is required.")
            .MaximumLength(200).WithMessage("Full name cannot exceed 200 characters.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.");

        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("Phone number is required.")
            .Matches(@"^\+?[0-9]{7,15}$").WithMessage("Phone number must be 7-15 digits.");

        RuleFor(x => x.CompanyId)
            .NotEmpty().WithMessage("Company ID is required.");

        RuleFor(x => x.Role)
            .IsInEnum().WithMessage("Invalid role specified.")
            .NotEqual(UserRole.SuperAdmin).WithMessage("Cannot assign SuperAdmin role via this endpoint.");

        RuleFor(x => x.TeamId)
            .NotEmpty()
            .When(x => x.Role == UserRole.TeamLeader)
            .WithMessage("Team ID is required for TeamLeader.");

        RuleFor(x => x.UnitId)
            .NotEmpty()
            .When(x => x.Role == UserRole.UnitLeader)
            .WithMessage("Unit ID is required for UnitLeader.");

        RuleFor(x => x.DepartmentId)
            .NotEmpty()
            .When(x => x.Role == UserRole.DepartmentManager || x.Role == UserRole.VicePresident)
            .WithMessage("Department ID is required for this role.");
    }
}
