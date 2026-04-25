using HrSystemApp.Application.Common;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Domain.Models;
using MediatR;

namespace HrSystemApp.Application.Features.Requests.Queries.GetRequestTypes;

public record GetRequestTypesQuery(Guid? CompanyId = null) : IRequest<Result<List<RequestTypeDto>>>;

public record RequestTypeDto(Guid Id, string KeyName, string DisplayName, bool IsSystemType, bool IsCustomType);

public class GetRequestTypesQueryHandler : IRequestHandler<GetRequestTypesQuery, Result<List<RequestTypeDto>>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetRequestTypesQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<List<RequestTypeDto>>> Handle(GetRequestTypesQuery request, CancellationToken cancellationToken)
    {
        // For now, return system types. When companyId is provided, include custom types too.
        var requestTypes = await _unitOfWork.RequestTypes.GetByCompanyAsync(request.CompanyId ?? Guid.Empty, cancellationToken);

        var dtos = requestTypes
            .Select(rt => new RequestTypeDto(rt.Id, rt.KeyName, rt.DisplayName, rt.IsSystemType, rt.IsCustomType))
            .OrderBy(rt => rt.DisplayName)
            .ToList();

        return Result.Success(dtos);
    }
}
