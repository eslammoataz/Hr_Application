using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.Errors;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Application.Interfaces.Services;
using MediatR;

namespace HrSystemApp.Application.Features.Requests.Queries.GetPendingApprovals;

public record GetPendingApprovalsQuery : IRequest<Result<PagedResult<PendingRequestDto>>>
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public RequestStatus? Status { get; set; }
    public RequestType? Type { get; set; }
}

public record PendingRequestDto
{
    public Guid Id { get; set; }
    public string RequesterName { get; set; } = string.Empty;
    public string RequesterCode { get; set; } = string.Empty;
    public RequestType Type { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? Details { get; set; }
}

public class GetPendingApprovalsQueryHandler : IRequestHandler<GetPendingApprovalsQuery, Result<PagedResult<PendingRequestDto>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public GetPendingApprovalsQueryHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result<PagedResult<PendingRequestDto>>> Handle(GetPendingApprovalsQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return Result.Failure<PagedResult<PendingRequestDto>>(DomainErrors.Auth.Unauthorized);

        var employee = await _unitOfWork.Employees.GetByUserIdAsync(userId, cancellationToken);
        if (employee == null)
            return Result.Failure<PagedResult<PendingRequestDto>>(DomainErrors.Employee.NotFound);

        var pendingRequests = await _unitOfWork.Requests.GetPendingApprovalsAsync(employee.Id, cancellationToken);
        var queryable = pendingRequests.AsQueryable();

        // Apply filters
        if (request.Status.HasValue)
            queryable = queryable.Where(r => r.Status == request.Status.Value);
        
        if (request.Type.HasValue)
            queryable = queryable.Where(r => r.RequestType == request.Type.Value);

        var totalCount = queryable.Count();
        var items = queryable
            .OrderByDescending(r => r.CreatedAt)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        var dtos = items.Select(r => new PendingRequestDto
        {
            Id = r.Id,
            RequesterName = r.Employee?.FullName ?? "Unknown",
            RequesterCode = r.Employee?.EmployeeCode ?? string.Empty,
            Type = r.RequestType,
            CreatedAt = r.CreatedAt,
            Details = r.Details
        }).ToList();

        return Result.Success(PagedResult<PendingRequestDto>.Create(dtos, request.PageNumber, request.PageSize, totalCount));
    }
}
