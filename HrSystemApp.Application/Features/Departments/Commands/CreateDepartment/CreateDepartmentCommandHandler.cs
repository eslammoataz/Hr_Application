using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Departments;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Domain.Models;
using MediatR;

namespace HrSystemApp.Application.Features.Departments.Commands.CreateDepartment;

public class CreateDepartmentCommandHandler : IRequestHandler<CreateDepartmentCommand, Result<DepartmentResponse>>
{
    private readonly IUnitOfWork _unitOfWork;

    public CreateDepartmentCommandHandler(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    public async Task<Result<DepartmentResponse>> Handle(CreateDepartmentCommand request, CancellationToken cancellationToken)
    {
        var companyExists = await _unitOfWork.Companies.ExistsAsync(c => c.Id == request.CompanyId, cancellationToken);
        if (!companyExists)
            return Result.Failure<DepartmentResponse>(DomainErrors.Company.NotFound);

        var nameExists = await _unitOfWork.Departments.ExistsAsync(
            d => d.CompanyId == request.CompanyId && d.Name == request.Name, cancellationToken);
        if (nameExists)
            return Result.Failure<DepartmentResponse>(DomainErrors.Department.AlreadyExists);

        var department = new Department
        {
            CompanyId = request.CompanyId,
            Name = request.Name,
            Description = request.Description,
            VicePresidentId = request.VicePresidentId,
            ManagerId = request.ManagerId
        };

        await _unitOfWork.Departments.AddAsync(department, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(MapToResponse(department, null, null));
    }

    internal static DepartmentResponse MapToResponse(Department d, string? vpName, string? managerName) =>
        new(d.Id, d.CompanyId, d.Name, d.Description, d.VicePresidentId, vpName, d.ManagerId, managerName, d.CreatedAt);
}
