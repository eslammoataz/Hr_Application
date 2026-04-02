using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.Errors;
using HrSystemApp.Domain.Enums;
using MediatR;

namespace HrSystemApp.Application.Features.Requests.Queries.GetUserRequests;

public record GetUserRequestsQuery : IRequest<Result<PagedResult<RequestDto>>>
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public RequestStatus? Status { get; set; }
    public RequestType? Type { get; set; }
}

public record RequestDto
{
    public Guid Id { get; set; }
    public RequestType Type { get; set; }
    public RequestStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? Details { get; set; }
    public string? CurrentApproverName { get; set; }
}

public class GetUserRequestsQueryHandler : IRequestHandler<GetUserRequestsQuery, Result<PagedResult<RequestDto>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public GetUserRequestsQueryHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result<PagedResult<RequestDto>>> Handle(GetUserRequestsQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) 
            return Result.Failure<PagedResult<RequestDto>>(DomainErrors.Auth.Unauthorized);

        var employee = await _unitOfWork.Employees.GetByUserIdAsync(userId, cancellationToken);
        if (employee == null) 
            return Result.Failure<PagedResult<RequestDto>>(DomainErrors.Employee.NotFound);

        var requests = await _unitOfWork.Requests.FindAsync(r => r.EmployeeId == employee.Id, cancellationToken);
        var queryable = requests.AsQueryable();

        if (request.Status.HasValue)
            queryable = queryable.Where(r => r.Status == request.Status.Value);

        if (request.Type.HasValue)
            queryable = queryable.Where(r => r.RequestType == request.Type.Value);

        var totalCount = queryable.Count();
        var items = queryable
            .OrderByDescending(r => r.CreatedAt)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList(); // Materialize to avoid lambda issues if IRequestRepository didn't include enough

        var mappedItems = items.Select(r => new RequestDto
        {
            Id = r.Id,
            Type = r.RequestType,
            Status = r.Status,
            CreatedAt = r.CreatedAt,
            Details = r.Details,
            CurrentApproverName = r.CurrentApprover != null ? r.CurrentApprover.FullName : "N/A"
        }).ToList();

        return Result.Success(PagedResult<RequestDto>.Create(mappedItems, request.PageNumber, request.PageSize, totalCount));
    }
}
