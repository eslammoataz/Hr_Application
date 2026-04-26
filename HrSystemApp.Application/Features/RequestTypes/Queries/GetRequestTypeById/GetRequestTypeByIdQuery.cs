using HrSystemApp.Application.Common;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using MediatR;

namespace HrSystemApp.Application.Features.RequestTypes.Queries;

public record GetRequestTypeByIdQuery(Guid Id) : IRequest<Result<RequestTypeDetailDto>>;

public record RequestTypeDetailDto(
    Guid Id,
    string KeyName,
    string DisplayName,
    bool IsSystemType,
    bool IsCustomType,
    Guid? CompanyId,
    string? FormSchemaJson,
    bool AllowExtraFields,
    string? RequestNumberPattern,
    int? DefaultSlaDays,
    string? DisplayNameLocalizationsJson,
    int Version);

public class GetRequestTypeByIdQueryHandler : IRequestHandler<GetRequestTypeByIdQuery, Result<RequestTypeDetailDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetRequestTypeByIdQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<RequestTypeDetailDto>> Handle(GetRequestTypeByIdQuery request, CancellationToken cancellationToken)
    {
        var requestType = await _unitOfWork.RequestTypes.GetByIdAsync(request.Id, cancellationToken);

        if (requestType == null)
            return Result.Failure<RequestTypeDetailDto>(DomainErrors.Requests.NotFound);

        return Result.Success(new RequestTypeDetailDto(
            requestType.Id,
            requestType.KeyName,
            requestType.DisplayName,
            requestType.IsSystemType,
            requestType.IsCustomType,
            requestType.CompanyId,
            requestType.FormSchemaJson,
            requestType.AllowExtraFields,
            requestType.RequestNumberPattern,
            requestType.DefaultSlaDays,
            requestType.DisplayNameLocalizationsJson,
            requestType.Version));
    }
}
