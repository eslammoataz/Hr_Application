using MediatR;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Companies;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Domain.Models;
using HrSystemApp.Domain.Enums;

namespace HrSystemApp.Application.Features.Companies.Commands.CreateCompany;

public class CreateCompanyCommandHandler : IRequestHandler<CreateCompanyCommand, Result<CompanyResponse>>
{
    private readonly IUnitOfWork _unitOfWork;

    public CreateCompanyCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CompanyResponse>> Handle(CreateCompanyCommand request, CancellationToken cancellationToken)
    {
        var company = new Company
        {
            CompanyName = request.CompanyName,
            CompanyLogoUrl = request.CompanyLogoUrl,
            YearlyVacationDays = request.YearlyVacationDays,
            Status = CompanyStatus.Active
        };

        await _unitOfWork.Companies.AddAsync(company, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new CompanyResponse(
            company.Id,
            company.CompanyName,
            company.CompanyLogoUrl,
            company.YearlyVacationDays,
            company.Status.ToString()
        ));
    }
}
