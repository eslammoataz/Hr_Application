using System.Text.Json;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.Features.Requests.Strategies;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using MediatR;
using Microsoft.Extensions.Logging;

namespace HrSystemApp.Application.Features.Requests.Commands.CreateRequest;

public record CreateRequestCommand(RequestType RequestType, JsonElement Data, string? Details = null) : IRequest<Result<Guid>>;

public class CreateRequestCommandHandler : IRequestHandler<CreateRequestCommand, Result<Guid>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IWorkflowService _workflowService;
    private readonly IRequestSchemaValidator _schemaValidator;
    private readonly IRequestStrategyFactory _strategyFactory;
    private readonly ILogger<CreateRequestCommandHandler> _logger;

    public CreateRequestCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IWorkflowService workflowService,
        IRequestSchemaValidator schemaValidator,
        IRequestStrategyFactory strategyFactory,
        ILogger<CreateRequestCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _workflowService = workflowService;
        _schemaValidator = schemaValidator;
        _strategyFactory = strategyFactory;
        _logger = logger;
    }

    public async Task<Result<Guid>> Handle(CreateRequestCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return Result.Failure<Guid>(new Error("Auth.Unauthorized", "User not authenticated."));

        var employee = await _unitOfWork.Employees.GetByUserIdAsync(userId, cancellationToken);
        if (employee == null)
        {
            _logger.LogWarning("CreateRequest failed: Employee profile not found for UserId {UserId}", userId);
            return Result.Failure<Guid>(new Error("Employee.NotFound", "Employee profile not found."));
        }

        _logger.LogInformation("Employee {EmployeeId} ({FullName}) found. Company: {CompanyId}",
            employee.Id, employee.FullName, employee.CompanyId);

        _logger.LogInformation("Attempting to create {RequestType} request for employee {EmployeeId} ({FullName})",
            request.RequestType, employee.Id, employee.FullName);

        var jsonData = request.Data.GetRawText();

        // 1. Resolve Definition
        var definition =
            await _unitOfWork.RequestDefinitions.GetByTypeAsync(employee.CompanyId, request.RequestType, cancellationToken);
        if (definition == null || !definition.IsActive)
        {
            _logger.LogWarning("{RequestType} is disabled or missing definition for company {CompanyId}", request.RequestType,
                employee.CompanyId);
            return Result.Failure<Guid>(new Error("Request.TypeDisabled",
                $"The {request.RequestType} request type is not available for your company."));
        }

        // 2. Structural Schema Validation
        var schemaResult = _schemaValidator.Validate(request.RequestType, jsonData, definition.FormSchemaJson);
        if (!schemaResult.IsSuccess)
        {
            _logger.LogWarning("Schema validation failed for {RequestType}. Error: {Error}", request.RequestType,
                schemaResult.Error.Message);
            return Result.Failure<Guid>(schemaResult.Error);
        }

        // 3. Business Logic Validation (Strategy)
        var strategy = _strategyFactory.GetStrategy(request.RequestType);
        if (strategy != null)
        {
            var strategyResult =
                await strategy.ValidateBusinessRulesAsync(employee.Id, request.Data, cancellationToken);
            if (!strategyResult.IsSuccess)
            {
                _logger.LogWarning("Business strategy validation failed for {RequestType}. Error: {Error}",
                    request.RequestType, strategyResult.Error.Message);
                return Result.Failure<Guid>(strategyResult.Error);
            }
        }

        // 4. Resolve Approval Workflow
        var approvalPath = await _workflowService.GetApprovalPathAsync(employee.Id, request.RequestType, cancellationToken);
        if (approvalPath.Count == 0)
        {
            _logger.LogWarning("Workflow resolution failed for {RequestType}: No active path found.", request.RequestType);
            return Result.Failure<Guid>(new Error("Workflow.NotFound",
                "No approval workflow defined for this request type."));
        }

        // 5. Persist Unified Request
        var newRequest = new Request
        {
            EmployeeId = employee.Id,
            RequestType = request.RequestType,
            Data = jsonData,
            Details = request.Details,
            Status = RequestStatus.Submitted,
            CurrentApproverId = approvalPath.First().Id,
            PlannedChainJson = JsonSerializer.Serialize(approvalPath.Select(a => new { a.Id, a.FullName }).ToList())
        };

        await _unitOfWork.Requests.AddAsync(newRequest, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Successfully created {RequestType} request {RequestId} with {StepCount} approval stages.",
            request.RequestType, newRequest.Id, approvalPath.Count);

        return Result.Success(newRequest.Id);
    }
}
