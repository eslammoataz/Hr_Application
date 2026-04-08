using HrSystemApp.Application.Common;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Domain.Enums;
using MediatR;

namespace HrSystemApp.Application.Features.Employees.Commands.ChangeEmployeeStatus;

public class ChangeEmployeeStatusCommandHandler : IRequestHandler<ChangeEmployeeStatusCommand, Result>
{
    private readonly IUnitOfWork _unitOfWork;

    public ChangeEmployeeStatusCommandHandler(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    public async Task<Result> Handle(ChangeEmployeeStatusCommand request, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(typeof(EmploymentStatus), request.Status))
            return Result.Failure(DomainErrors.Employee.InvalidEmploymentStatus);

        var employee = await _unitOfWork.Employees.GetByIdAsync(request.Id, cancellationToken);
        if (employee is null)
            return Result.Failure(DomainErrors.Employee.NotFound);

        var targetStatus = (EmploymentStatus)request.Status;
        employee.EmploymentStatus = targetStatus;

        if (employee.UserId is not null)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(employee.UserId, cancellationToken);
            if (user is not null)
            {
                user.IsActive = targetStatus is not (
                    EmploymentStatus.Inactive or
                    EmploymentStatus.Suspended or
                    EmploymentStatus.Terminated);
                await _unitOfWork.Users.UpdateAsync(user, cancellationToken);
            }
        }

        await _unitOfWork.Employees.UpdateAsync(employee, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
