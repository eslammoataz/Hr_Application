using FluentValidation;

namespace HrSystemApp.Application.Features.Employees.Commands.AssignEmployeeToTeam;

public class AssignEmployeeToTeamCommandValidator : AbstractValidator<AssignEmployeeToTeamCommand>
{
    public AssignEmployeeToTeamCommandValidator()
    {
        RuleFor(x => x.EmployeeId).NotEmpty();
        RuleFor(x => x.TeamId).NotEmpty();
    }
}
