using FluentValidation;
using HrSystemApp.Application.Resources;
using HrSystemApp.Domain.Enums;

namespace HrSystemApp.Application.Features.Employees.Commands.CreateEmployee;

public class CreateEmployeeCommandValidator : AbstractValidator<CreateEmployeeCommand>
{
    public CreateEmployeeCommandValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage(Messages.Validation.FullNameRequired)
            .MaximumLength(200).WithMessage(Messages.Validation.FullNameMaxLength);

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage(Messages.Validation.EmailRequired)
            .EmailAddress().WithMessage(Messages.Validation.ValidEmailRequired);

        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage(Messages.Validation.PhoneRequired)
            .Matches(@"^\+?[0-9]{7,15}$").WithMessage(Messages.Validation.PhoneMustBeDigits);

        RuleFor(x => x.CompanyId)
            .NotEmpty().WithMessage(Messages.Validation.CompanyIdRequired);

        RuleFor(x => x.Role)
            .IsInEnum().WithMessage(Messages.Validation.InvalidRole)
            .NotEqual(UserRole.SuperAdmin).WithMessage(Messages.Validation.CannotAssignSuperAdmin);

        RuleFor(x => x.TeamId)
            .NotEmpty()
            .When(x => x.Role == UserRole.TeamLeader)
            .WithMessage(Messages.Validation.TeamIdRequiredForTeamLeader);

        RuleFor(x => x.UnitId)
            .NotEmpty()
            .When(x => x.Role == UserRole.UnitLeader)
            .WithMessage(Messages.Validation.UnitIdRequiredForUnitLeader);

        RuleFor(x => x.DepartmentId)
            .NotEmpty()
            .When(x => x.Role == UserRole.DepartmentManager || x.Role == UserRole.VicePresident)
            .WithMessage(Messages.Validation.DepartmentIdRequired);
    }
}
