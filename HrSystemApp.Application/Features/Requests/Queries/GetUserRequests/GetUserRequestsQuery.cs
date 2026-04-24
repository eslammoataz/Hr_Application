using System.Text.Json;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Requests;
using HrSystemApp.Application.Errors;
using HrSystemApp.Domain.Enums;
using MediatR;

namespace HrSystemApp.Application.Features.Requests.Queries.GetUserRequests;

public record GetUserRequestsQuery : IRequest<Result<PagedResult<RequestDto>>>
{
    private const int MaxPageSize = 100;
    private int _pageSize = 10;

    public int PageNumber { get; set; } = 1;

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value > MaxPageSize ? MaxPageSize : value < 1 ? 1 : value;
    }

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

    /// <summary>
    /// Current step order (1-based). 0 means fully approved or no steps.
    /// </summary>
    public int CurrentStepOrder { get; set; }

    /// <summary>
    /// Total number of approval steps.
    /// </summary>
    public int TotalSteps { get; set; }
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

        var queryable = _unitOfWork.Requests.QueryByEmployeeId(employee.Id);

        if (request.Status.HasValue)
            queryable = queryable.Where(r => r.Status == request.Status.Value);

        if (request.Type.HasValue)
            queryable = queryable.Where(r => r.RequestType == request.Type.Value);

        var totalCount = await _unitOfWork.Requests.CountAsync(queryable, cancellationToken);
        var items = await _unitOfWork.Requests.ToListAsync(
            queryable.OrderByDescending(r => r.CreatedAt)
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize),
            cancellationToken);

        var mappedItems = items.Select(r =>
        {
            var plannedSteps = JsonSerializer.Deserialize<List<PlannedStepDto>>(r.PlannedStepsJson ?? "[]") ?? new List<PlannedStepDto>();
            return new RequestDto
            {
                Id = r.Id,
                Type = r.RequestType,
                Status = r.Status,
                CreatedAt = r.CreatedAt,
                Details = r.Details,
                CurrentStepOrder = r.CurrentStepOrder,
                TotalSteps = plannedSteps.Count
            };
        }).ToList();

        return Result.Success(PagedResult<RequestDto>.Create(mappedItems, request.PageNumber, request.PageSize, totalCount));
    }
}
