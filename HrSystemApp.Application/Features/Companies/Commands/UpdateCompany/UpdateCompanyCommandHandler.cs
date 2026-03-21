using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Companies;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using Mapster;
using MediatR;

namespace HrSystemApp.Application.Features.Companies.Commands.UpdateCompany;

public class UpdateCompanyCommandHandler : IRequestHandler<UpdateCompanyCommand, Result<CompanyResponse>>
{
    private readonly IUnitOfWork _unitOfWork;

    public UpdateCompanyCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CompanyResponse>> Handle(UpdateCompanyCommand request, CancellationToken cancellationToken)
    {
        var company = await _unitOfWork.Companies.GetByIdAsync(request.Id, cancellationToken);
        if (company is null)
            return Result.Failure<CompanyResponse>(DomainErrors.General.NotFound);

        company.CompanyName = request.CompanyName;
        company.CompanyLogoUrl = request.CompanyLogoUrl;
        company.YearlyVacationDays = request.YearlyVacationDays;
        company.Status = request.Status;

        await _unitOfWork.Companies.UpdateAsync(company, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(company.Adapt<CompanyResponse>());
    }
}
