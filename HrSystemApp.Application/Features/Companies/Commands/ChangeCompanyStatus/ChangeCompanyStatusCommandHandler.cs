using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Companies;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using Mapster;
using MediatR;

namespace HrSystemApp.Application.Features.Companies.Commands.ChangeCompanyStatus;

public class ChangeCompanyStatusCommandHandler : IRequestHandler<ChangeCompanyStatusCommand, Result<CompanyResponse>>
{
    private readonly IUnitOfWork _unitOfWork;

    public ChangeCompanyStatusCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CompanyResponse>> Handle(ChangeCompanyStatusCommand request, CancellationToken cancellationToken)
    {
        var company = await _unitOfWork.Companies.GetByIdAsync(request.Id, cancellationToken);
        if (company is null)
        {
            return Result.Failure<CompanyResponse>(DomainErrors.General.NotFound);
        }

        company.Status = request.Status;

        await _unitOfWork.Companies.UpdateAsync(company, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(company.Adapt<CompanyResponse>());
    }
}
