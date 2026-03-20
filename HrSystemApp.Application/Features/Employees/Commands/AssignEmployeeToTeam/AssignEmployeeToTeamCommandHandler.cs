using HrSystemApp.Application.Common;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using MediatR;

namespace HrSystemApp.Application.Features.Employees.Commands.AssignEmployeeToTeam;

public class AssignEmployeeToTeamCommandHandler : IRequestHandler<AssignEmployeeToTeamCommand, Result>
{
    private readonly IUnitOfWork _unitOfWork;

    public AssignEmployeeToTeamCommandHandler(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    public async Task<Result> Handle(AssignEmployeeToTeamCommand request, CancellationToken cancellationToken)
    {
        var employee = await _unitOfWork.Employees.GetByIdAsync(request.EmployeeId, cancellationToken);
        if (employee is null)
            return Result.Failure(DomainErrors.Employee.NotFound);

        var team = await _unitOfWork.Teams.GetByIdAsync(request.TeamId, cancellationToken);
        if (team is null)
            return Result.Failure(DomainErrors.Team.NotFound);

        // Resolve unit and department from team hierarchy
        var unit = await _unitOfWork.Units.GetByIdAsync(team.UnitId, cancellationToken);
        employee.TeamId = team.Id;
        employee.UnitId = team.UnitId;
        employee.DepartmentId = unit?.DepartmentId;

        await _unitOfWork.Employees.UpdateAsync(employee, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
