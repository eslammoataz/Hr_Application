using Mapster;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Employees;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Domain.Models;
using MediatR;

namespace HrSystemApp.Application.Features.Employees.Commands.UpdateEmployee;

public class UpdateEmployeeCommandHandler : IRequestHandler<UpdateEmployeeCommand, Result<EmployeeResponse>>
{
    private readonly IUnitOfWork _unitOfWork;

    public UpdateEmployeeCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<EmployeeResponse>> Handle(UpdateEmployeeCommand request, CancellationToken cancellationToken)
    {
        var employee = await _unitOfWork.Employees.GetWithDetailsAsync(request.Id, cancellationToken);
        if (employee is null)
            return Result.Failure<EmployeeResponse>(DomainErrors.Employee.NotFound);

        request.Adapt(employee);

        await _unitOfWork.Employees.UpdateAsync(employee, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(employee.Adapt<EmployeeResponse>());
    }
}
