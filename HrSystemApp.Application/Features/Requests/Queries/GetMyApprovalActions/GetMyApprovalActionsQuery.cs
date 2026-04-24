using HrSystemApp.Application.Common;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Enums;
using MediatR;

namespace HrSystemApp.Application.Features.Requests.Queries.GetMyApprovalActions;

public record GetMyApprovalActionsQuery : IRequest<Result<PagedResult<ApprovalActionDto>>>
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;

    /// <summary>Filter by the action the approver took (Approved / Rejected). Null = all.</summary>
    public RequestStatus? ActionStatus { get; set; }

    /// <summary>Filter by request type. Null = all.</summary>
    public RequestType? RequestType { get; set; }
}

public record ApprovalActionDto
{
    public Guid RequestId { get; set; }
    public RequestType RequestType { get; set; }
    public string RequestTypeName { get; set; } = string.Empty;

    public string RequesterName { get; set; } = string.Empty;
    public string RequesterCode { get; set; } = string.Empty;
    public Guid RequesterId { get; set; }

    /// <summary>Action taken by this approver: Approved or Rejected.</summary>
    public RequestStatus ActionTaken { get; set; }
    public string ActionTakenName { get; set; } = string.Empty;

    public string? Comment { get; set; }

    /// <summary>When the approver took the action.</summary>
    public DateTime ActionAt { get; set; }

    /// <summary>Current overall status of the request (may have progressed further after approval).</summary>
    public RequestStatus CurrentRequestStatus { get; set; }
    public string CurrentRequestStatusName { get; set; } = string.Empty;
}

public class GetMyApprovalActionsQueryHandler
    : IRequestHandler<GetMyApprovalActionsQuery, Result<PagedResult<ApprovalActionDto>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public GetMyApprovalActionsQueryHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result<PagedResult<ApprovalActionDto>>> Handle(
        GetMyApprovalActionsQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return Result.Failure<PagedResult<ApprovalActionDto>>(DomainErrors.Auth.Unauthorized);

        var employee = await _unitOfWork.Employees.GetByUserIdAsync(userId, cancellationToken);
        if (employee is null)
            return Result.Failure<PagedResult<ApprovalActionDto>>(DomainErrors.Employee.NotFound);

        var queryable = _unitOfWork.Requests.QueryApprovalActions(employee.Id);

        if (request.ActionStatus.HasValue)
            queryable = queryable.Where(h => h.Status == request.ActionStatus.Value);

        if (request.RequestType.HasValue)
            queryable = queryable.Where(h => h.Request.RequestType == request.RequestType.Value);

        var totalCount = await _unitOfWork.Requests.CountHistoryAsync(queryable, cancellationToken);

        var items = await _unitOfWork.Requests.ToListHistoryAsync(
            queryable.OrderByDescending(h => h.CreatedAt)
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize),
            cancellationToken);

        var dtos = items.Select(h => new ApprovalActionDto
        {
            RequestId              = h.RequestId,
            RequestType            = h.Request.RequestType,
            RequestTypeName        = h.Request.RequestType.ToString(),
            RequesterId            = h.Request.EmployeeId,
            RequesterName          = h.Request.Employee?.FullName ?? "Unknown",
            RequesterCode          = h.Request.Employee?.EmployeeCode ?? string.Empty,
            ActionTaken            = h.Status,
            ActionTakenName        = h.Status.ToString(),
            Comment                = h.Comment,
            ActionAt               = h.CreatedAt,
            CurrentRequestStatus   = h.Request.Status,
            CurrentRequestStatusName = h.Request.Status.ToString(),
        }).ToList();

        return Result.Success(PagedResult<ApprovalActionDto>.Create(dtos, request.PageNumber, request.PageSize, totalCount));
    }
}
