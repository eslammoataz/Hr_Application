using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Companies;
using HrSystemApp.Application.Interfaces;
using Mapster;
using MediatR;

namespace HrSystemApp.Application.Features.Companies.Queries.GetCompanies;

public class GetCompaniesQueryHandler : IRequestHandler<GetCompaniesQuery, Result<PagedResult<CompanyResponse>>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetCompaniesQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<PagedResult<CompanyResponse>>> Handle(GetCompaniesQuery request, CancellationToken cancellationToken)
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

        return Result.Success(PagedResult<CompanyResponse>.Create(
            items, paged.PageNumber, paged.PageSize, paged.TotalCount));
    }
}
