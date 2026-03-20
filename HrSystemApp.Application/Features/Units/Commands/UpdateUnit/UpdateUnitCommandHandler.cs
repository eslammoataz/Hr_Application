using AutoMapper;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Units;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using MediatR;

namespace HrSystemApp.Application.Features.Units.Commands.UpdateUnit;

public class UpdateUnitCommandHandler : IRequestHandler<UpdateUnitCommand, Result<UnitResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public UpdateUnitCommandHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<Result<UnitResponse>> Handle(UpdateUnitCommand request, CancellationToken cancellationToken)
    {
        var unit = await _unitOfWork.Units.GetByIdAsync(request.Id, cancellationToken);
        if (unit is null)
            return Result.Failure<UnitResponse>(DomainErrors.Unit.NotFound);

        if (request.Name is not null && request.Name != unit.Name)
        {
            var nameExists = await _unitOfWork.Units.ExistsAsync(
                u => u.DepartmentId == unit.DepartmentId && u.Name == request.Name && u.Id != request.Id,
                cancellationToken);
            if (nameExists)
                return Result.Failure<UnitResponse>(DomainErrors.Unit.AlreadyExists);
        }

        _mapper.Map(request, unit);

        await _unitOfWork.Units.UpdateAsync(unit, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(_mapper.Map<UnitResponse>(unit));
    }
}
