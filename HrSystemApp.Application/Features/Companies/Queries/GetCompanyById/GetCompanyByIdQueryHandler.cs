using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Companies;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using Mapster;
using MediatR;

namespace HrSystemApp.Application.Features.Companies.Queries.GetCompanyById;

public class GetCompanyByIdQueryHandler : IRequestHandler<GetCompanyByIdQuery, Result<CompanyResponse>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetCompanyByIdQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CompanyResponse>> Handle(GetCompanyByIdQuery request, CancellationToken cancellationToken)
    {
        var company = await _unitOfWork.Companies.GetWithDetailsAsync(
            request.Id,
            request.IncludeLocations,
            cancellationToken);

        if (company is null)
            return Result.Failure<CompanyResponse>(DomainErrors.General.NotFound);

        return Result.Success(company.Adapt<CompanyResponse>());
    }
}
