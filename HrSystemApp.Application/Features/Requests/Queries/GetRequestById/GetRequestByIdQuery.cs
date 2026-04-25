using System.Text.Json;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.Common.Logging;
using HrSystemApp.Application.DTOs.Requests;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HrSystemApp.Application.Features.Requests.Queries.GetRequestById;

public record GetRequestByIdQuery(Guid Id) : IRequest<Result<RequestDetailDto>>;

public record RequestDetailDto
{
    public Guid Id { get; set; }
    public RequestType Type { get; set; }
    public RequestStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public string RequesterName { get; set; } = string.Empty;
    public string? Details { get; set; }

    /// <summary>
    /// Type-specific JSON data (e.g. { "startDate": "...", "duration": 5 })
    /// </summary>
    public object Data { get; set; } = new { };

    /// <summary>
    /// Current step order (1-based). 0 means approved.
    /// </summary>
    public int CurrentStepOrder { get; set; }

    public List<ApprovalHistoryDto> History { get; set; } = new();
    public List<PlannedStepDto> PlannedSteps { get; set; } = new();
}

public record ApprovalHistoryDto(string ApproverName, Guid ApproverId, RequestStatus Status, DateTime CreatedAt, string? Comment);

public class GetRequestByIdQueryHandler : IRequestHandler<GetRequestByIdQuery, Result<RequestDetailDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<GetRequestByIdQueryHandler> _logger;
    private readonly LoggingOptions _loggingOptions;

    public GetRequestByIdQueryHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ILogger<GetRequestByIdQueryHandler> logger,
        IOptions<LoggingOptions> loggingOptions)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _logger = logger;
        _loggingOptions = loggingOptions.Value;
    }

    public async Task<Result<RequestDetailDto>> Handle(GetRequestByIdQuery request, CancellationToken cancellationToken)
    {

        var existingRequest = await _unitOfWork.Requests.GetByIdWithHistoryAsync(request.Id, cancellationToken);
        if (existingRequest == null)
        {
            _logger.LogDecision(_loggingOptions, LogAction.Workflow.CreateRequest, LogStage.Validation,
                "RequestNotFound", new { RequestId = request.Id });
            return Result.Failure<RequestDetailDto>(DomainErrors.Requests.NotFound);
        }

        var userId = _currentUserService.UserId;
        var currentUserRole = _currentUserService.Role;
        var isHrOrAbove = currentUserRole is not null &&
            Enum.TryParse<UserRole>(currentUserRole, out var role) &&
            role is UserRole.SuperAdmin or UserRole.Executive or UserRole.HR or UserRole.CompanyAdmin;

        var employee = await _unitOfWork.Employees.GetByUserIdAsync(userId, cancellationToken);
        var isRequester = employee is not null && existingRequest.EmployeeId == employee.Id;

        var plannedSteps = JsonSerializer.Deserialize<List<PlannedStepDto>>(existingRequest.PlannedStepsJson ?? "[]") ?? new List<PlannedStepDto>();
        var isApprover = plannedSteps.Any(s => s.Approvers.Any(a => a.EmployeeId == employee?.Id));

        if (isHrOrAbove && (employee is null || existingRequest.Employee.CompanyId != employee.CompanyId))
            isHrOrAbove = false;

        if (!isHrOrAbove && !isRequester && !isApprover)
        {
            return Result.Failure<RequestDetailDto>(DomainErrors.Auth.Unauthorized);
        }

        var dto = new RequestDetailDto
        {
            Id = existingRequest.Id,
            Type = existingRequest.RequestType,
            Status = existingRequest.Status,
            CreatedAt = existingRequest.CreatedAt,
            RequesterName = existingRequest.Employee.FullName,
            Details = existingRequest.Details,
            CurrentStepOrder = existingRequest.CurrentStepOrder,

            History = existingRequest.ApprovalHistory.Select(h => new ApprovalHistoryDto(
                h.Approver.FullName,
                h.ApproverId,
                h.Status,
                h.CreatedAt,
                h.Comment
            )).ToList(),

            Data = JsonSerializer.Deserialize<object>(existingRequest.Data) ?? new { },
            PlannedSteps = plannedSteps
        };

        return Result.Success(dto);
    }
}
