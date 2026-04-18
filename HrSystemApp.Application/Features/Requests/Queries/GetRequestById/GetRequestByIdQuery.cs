using System.Text.Json;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Requests;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

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
    private readonly ILogger<GetRequestByIdQueryHandler> _logger;

    public GetRequestByIdQueryHandler(IUnitOfWork unitOfWork, ILogger<GetRequestByIdQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<RequestDetailDto>> Handle(GetRequestByIdQuery request, CancellationToken cancellationToken)
    {
        var existingRequest = await _unitOfWork.Requests.GetByIdWithHistoryAsync(request.Id, cancellationToken);
        if (existingRequest == null)
        {
            _logger.LogWarning("GetRequestById failed: Request {RequestId} not found.", request.Id);
            return Result.Failure<RequestDetailDto>(DomainErrors.Requests.NotFound);
        }

        _logger.LogInformation("Retrieving details for request {RequestId} of type {Type}", existingRequest.Id, existingRequest.RequestType);

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
            PlannedSteps = JsonSerializer.Deserialize<List<PlannedStepDto>>(existingRequest.PlannedStepsJson ?? "[]") ?? new List<PlannedStepDto>()
        };

        return Result.Success(dto);
    }
}