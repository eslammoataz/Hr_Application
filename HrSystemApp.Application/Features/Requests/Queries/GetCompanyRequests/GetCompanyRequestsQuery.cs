using System.Text.Json;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Requests;
using HrSystemApp.Application.Errors;
using HrSystemApp.Domain.Enums;
using MediatR;

namespace HrSystemApp.Application.Features.Requests.Queries.GetCompanyRequests;

public record GetCompanyRequestsQuery : IRequest<Result<PagedResult<AdminRequestDto>>>
{
    private const int MaxPageSize = 100;
    private int _pageSize = 10;
    private int _pageNumber = 1;

    public int PageNumber
    {
        get => _pageNumber;
        set => _pageNumber = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value > MaxPageSize ? MaxPageSize : value < 1 ? 1 : value;
    }

    public RequestStatus? Status { get; set; }
    public RequestType? Type { get; set; }
}

public record AdminRequestDto
{
    public Guid Id { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeCode { get; set; } = string.Empty;
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

public class GetCompanyRequestsQueryHandler : IRequestHandler<GetCompanyRequestsQuery, Result<PagedResult<AdminRequestDto>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public GetCompanyRequestsQueryHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result<PagedResult<AdminRequestDto>>> Handle(GetCompanyRequestsQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return Result.Failure<PagedResult<AdminRequestDto>>(DomainErrors.Auth.Unauthorized);

        var admin = await _unitOfWork.Employees.GetByUserIdAsync(userId, cancellationToken);
        if (admin == null)
            return Result.Failure<PagedResult<AdminRequestDto>>(DomainErrors.Employee.NotFound);

        var queryable = _unitOfWork.Requests.QueryByCompanyId(admin.CompanyId);

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

        var dtos = items.Select(r =>
        {
            var plannedSteps = JsonSerializer.Deserialize<List<PlannedStepDto>>(r.PlannedStepsJson ?? "[]") ?? new List<PlannedStepDto>();
            return new AdminRequestDto
            {
                Id = r.Id,
                EmployeeName = r.Employee?.FullName ?? "Unknown",
                EmployeeCode = r.Employee?.EmployeeCode ?? string.Empty,
                Type = r.RequestType,
                Status = r.Status,
                CreatedAt = r.CreatedAt,
                Details = r.Details,
                CurrentStepOrder = r.CurrentStepOrder,
                TotalSteps = plannedSteps.Count
            };
        }).ToList();

        return Result.Success(PagedResult<AdminRequestDto>.Create(dtos, request.PageNumber, request.PageSize, totalCount));
    }
}
