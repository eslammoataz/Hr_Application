using MediatR;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;

namespace HrSystemApp.Application.Features.Companies.Commands.DeleteCompanyLocation;

public class DeleteCompanyLocationCommandHandler : IRequestHandler<DeleteCompanyLocationCommand, Result<Guid>>
{
    private readonly IUnitOfWork _unitOfWork;

    public DeleteCompanyLocationCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Guid>> Handle(DeleteCompanyLocationCommand request, CancellationToken cancellationToken)
    {
        var location = await _unitOfWork.CompanyLocations.GetByIdAsync(request.LocationId, cancellationToken);
        if (location == null)
        {
            return Result.Failure<Guid>(DomainErrors.General.NotFound);
        }

        await _unitOfWork.CompanyLocations.DeleteAsync(location, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(location.Id);
    }
}
