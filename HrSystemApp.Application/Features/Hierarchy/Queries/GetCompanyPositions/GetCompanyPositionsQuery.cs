using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Companies;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using MediatR;

namespace HrSystemApp.Application.Features.Hierarchy.Queries.GetCompanyPositions;

public record GetCompanyPositionsQuery() : IRequest<Result<IReadOnlyList<CompanyPositionResponse>>>;

public class GetCompanyPositionsQueryHandler : IRequestHandler<GetCompanyPositionsQuery, Result<IReadOnlyList<CompanyPositionResponse>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public GetCompanyPositionsQueryHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result<IReadOnlyList<CompanyPositionResponse>>> Handle(GetCompanyPositionsQuery request, CancellationToken cancellationToken)
    {
        var companyId = _currentUserService.CompanyId;

        if (!companyId.HasValue || companyId == Guid.Empty)
        {
            return Result.Success<IReadOnlyList<CompanyPositionResponse>>(new List<CompanyPositionResponse>());
        }

        var positions = await _unitOfWork.HierarchyPositions.GetByCompanyAsync(companyId.Value, cancellationToken);
        
        var response = positions.Select(p => new CompanyPositionResponse(
            p.Role,
            p.PositionTitle,
            p.SortOrder
        )).ToList();

        return Result.Success<IReadOnlyList<CompanyPositionResponse>>(response);
    }
}
