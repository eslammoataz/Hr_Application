using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Units;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using MediatR;
using Unit = HrSystemApp.Domain.Models.Unit;

namespace HrSystemApp.Application.Features.Units.Commands.CreateUnit;

public class CreateUnitCommandHandler : IRequestHandler<CreateUnitCommand, Result<UnitResponse>>
{
    private readonly IUnitOfWork _unitOfWork;

    public CreateUnitCommandHandler(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    public async Task<Result<UnitResponse>> Handle(CreateUnitCommand request, CancellationToken cancellationToken)
    {
        var department = await _unitOfWork.Departments.GetByIdAsync(request.DepartmentId, cancellationToken);
        if (department is null)
            return Result.Failure<UnitResponse>(DomainErrors.Department.NotFound);

        var nameExists = await _unitOfWork.Units.ExistsAsync(
            u => u.DepartmentId == request.DepartmentId && u.Name == request.Name, cancellationToken);
        if (nameExists)
            return Result.Failure<UnitResponse>(DomainErrors.Unit.AlreadyExists);

        var unit = new Unit
        {
            DepartmentId = request.DepartmentId,
            Name = request.Name,
            Description = request.Description,
            UnitLeaderId = request.UnitLeaderId
        };

        await _unitOfWork.Units.AddAsync(unit, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new UnitResponse(
            unit.Id, unit.DepartmentId, department.Name, unit.Name,
            unit.Description, unit.UnitLeaderId, null, unit.CreatedAt));
    }
}
