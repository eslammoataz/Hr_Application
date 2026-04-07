using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Companies;
using HrSystemApp.Application.Interfaces;
using Mapster;
using MediatR;

namespace HrSystemApp.Application.Features.Companies.Queries.GetCompanies;

public class GetCompaniesQueryHandler : IRequestHandler<GetCompaniesQuery, Result<CompaniesPagedResult>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetCompaniesQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CompaniesPagedResult>> Handle(GetCompaniesQuery request, CancellationToken cancellationToken)
    {
        var paged = await _unitOfWork.Companies.GetPagedAsync(
            request.SearchTerm, 
            request.Status,
            request.PageNumber, 
            request.PageSize, 
            request.IncludeLocations, 
            request.IncludeDepartments, 
            cancellationToken);
        
        var items = paged.Items.Adapt<List<CompanyResponse>>();

        var (active, inactive, suspended) = await _unitOfWork.Companies.GetStatusCountsAsync(cancellationToken);

        return Result.Success(new CompaniesPagedResult
        {
            Items = items,
            PageNumber = paged.PageNumber,
            PageSize = paged.PageSize,
            TotalCount = paged.TotalCount,
            TotalActive = active,
            TotalInactive = inactive,
            TotalSuspended = suspended
        });
    }
}
