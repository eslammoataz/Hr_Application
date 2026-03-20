using HrSystemApp.Application.Common;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Domain.Enums;
using MediatR;

namespace HrSystemApp.Application.Features.Employees.Commands.DeactivateEmployee;

public class DeactivateEmployeeCommandHandler : IRequestHandler<DeactivateEmployeeCommand, Result>
{
    private readonly IUnitOfWork _unitOfWork;

    public DeactivateEmployeeCommandHandler(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    public async Task<Result> Handle(DeactivateEmployeeCommand request, CancellationToken cancellationToken)
    {
        var employee = await _unitOfWork.Employees.GetByIdAsync(request.Id, cancellationToken);
        if (employee is null)
            return Result.Failure(DomainErrors.Employee.NotFound);

        if (employee.EmploymentStatus == EmploymentStatus.Inactive)
            return Result.Failure(DomainErrors.Employee.AlreadyInactive);

        employee.EmploymentStatus = EmploymentStatus.Inactive;

        if (employee.UserId is not null)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(employee.UserId, cancellationToken);
            if (user is not null)
            {
                user.IsActive = false;
                await _unitOfWork.Users.UpdateAsync(user, cancellationToken);
            }
        }

        await _unitOfWork.Employees.UpdateAsync(employee, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
