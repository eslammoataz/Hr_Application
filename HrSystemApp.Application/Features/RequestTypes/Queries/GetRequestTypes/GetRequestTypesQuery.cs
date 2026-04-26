using HrSystemApp.Application.Common;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Repositories;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Models;
using MediatR;

namespace HrSystemApp.Application.Features.RequestTypes.Queries;

public record GetRequestTypesQuery(Guid? CompanyId = null) : IRequest<Result<List<RequestTypeDto>>>;

public record RequestTypeDto(
    Guid Id,
    string KeyName,
    string DisplayName,
    bool IsSystemType,
    bool IsCustomType,
    string? FormSchemaJson,
    bool AllowExtraFields,
    string? RequestNumberPattern,
    int? DefaultSlaDays);

public class GetRequestTypesQueryHandler : IRequestHandler<GetRequestTypesQuery, Result<List<RequestTypeDto>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public GetRequestTypesQueryHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result<List<RequestTypeDto>>> Handle(GetRequestTypesQuery request, CancellationToken cancellationToken)
    {
        var companyId = request.CompanyId;

        // If no companyId provided, resolve from the authenticated user's employee
        if (!companyId.HasValue || companyId == Guid.Empty)
        {
            var userId = _currentUserService.UserId;
            if (!string.IsNullOrEmpty(userId))
            {
                var employee = await _unitOfWork.Employees.GetByUserIdAsync(userId, cancellationToken);
                if (employee != null)
                    companyId = employee.CompanyId;
            }
        }

        var requestTypes = await _unitOfWork.RequestTypes.GetByCompanyAsync(companyId ?? Guid.Empty, cancellationToken);

        var dtos = requestTypes
            .Select(rt => new RequestTypeDto(
                rt.Id,
                rt.KeyName,
                rt.DisplayName,
                rt.IsSystemType,
                rt.IsCustomType,
                rt.FormSchemaJson,
                rt.AllowExtraFields,
                rt.RequestNumberPattern,
                rt.DefaultSlaDays))
            .ToList();

        return Result.Success(dtos);
    }
}
