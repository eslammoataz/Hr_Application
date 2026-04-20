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
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
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

        // Query all requests for the company
        var requests = await _unitOfWork.Requests.FindAsync(
            r => r.Employee.CompanyId == admin.CompanyId,
            cancellationToken);

        var queryable = requests.AsQueryable();

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
