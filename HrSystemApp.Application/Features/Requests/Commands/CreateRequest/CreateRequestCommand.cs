using System.Text.Json;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Requests;
using HrSystemApp.Application.Errors;
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
    private readonly IWorkflowResolutionService _workflowResolutionService;
    private readonly IRequestSchemaValidator _schemaValidator;
    private readonly IRequestStrategyFactory _strategyFactory;
    private readonly ILogger<CreateRequestCommandHandler> _logger;

    public CreateRequestCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IWorkflowResolutionService workflowResolutionService,
        IRequestSchemaValidator schemaValidator,
        IRequestStrategyFactory strategyFactory,
        ILogger<CreateRequestCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _workflowResolutionService = workflowResolutionService;
        _schemaValidator = schemaValidator;
        _strategyFactory = strategyFactory;
        _logger = logger;
    }

    public async Task<Result<Guid>> Handle(CreateRequestCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return Result.Failure<Guid>(DomainErrors.Auth.Unauthorized);

        var employee = await _unitOfWork.Employees.GetByUserIdAsync(userId, cancellationToken);
        if (employee == null)
        {
            _logger.LogWarning("CreateRequest failed: Employee profile not found for UserId {UserId}", userId);
            return Result.Failure<Guid>(DomainErrors.Employee.NotFound);
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
            return Result.Failure<Guid>(DomainErrors.Requests.TypeDisabled);
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

        // 4. Get employee's OrgNode assignment
        var assignment = await _unitOfWork.OrgNodeAssignments.GetByEmployeeWithNodeAsync(employee.Id, cancellationToken);
        if (assignment == null)
        {
            _logger.LogWarning("CreateRequest failed: Employee {EmployeeId} is not assigned to any OrgNode.", employee.Id);
            return Result.Failure<Guid>(DomainErrors.OrgNode.NotFound);
        }

        // 5. Build workflow steps from definition (map all fields)
        var definitionSteps = definition.WorkflowSteps
            .Select(s => new WorkflowStepDto
            {
                StepType = s.StepType,
                OrgNodeId = s.OrgNodeId,
                BypassHierarchyCheck = s.BypassHierarchyCheck,
                DirectEmployeeId = s.DirectEmployeeId,
                SortOrder = s.SortOrder
            })
            .ToList();

        // 5b. Submission-time validation: ensure all referenced entities still exist and belong to this company
        foreach (var step in definitionSteps)
        {
            if (step.StepType == WorkflowStepType.OrgNode && step.OrgNodeId.HasValue)
            {
                var node = await _unitOfWork.OrgNodes.GetByIdAsync(step.OrgNodeId.Value, cancellationToken);
                if (node == null || node.CompanyId != employee.CompanyId)
                    return Result.Failure<Guid>(DomainErrors.Request.OrgNodeNotInCompany);
            }
            else if (step.StepType == WorkflowStepType.DirectEmployee && step.DirectEmployeeId.HasValue)
            {
                var directEmp = await _unitOfWork.Employees.GetByIdAsync(step.DirectEmployeeId.Value, cancellationToken);
                if (directEmp == null || directEmp.CompanyId != employee.CompanyId)
                    return Result.Failure<Guid>(DomainErrors.Request.DirectEmployeeNotInCompany);

                // Also check employee is still active
                if (directEmp.EmploymentStatus != EmploymentStatus.Active)
                    return Result.Failure<Guid>(DomainErrors.Request.DirectEmployeeNotActive);
            }
        }

        // 6. Build approval chain (validates steps and populates approvers)
        var approvalChainResult = await _workflowResolutionService.BuildApprovalChainAsync(
            employee.Id,
            assignment.OrgNodeId,
            definitionSteps,
            cancellationToken);

        if (!approvalChainResult.IsSuccess)
        {
            _logger.LogWarning("Workflow resolution failed for {RequestType}: {Error}",
                request.RequestType, approvalChainResult.Error.Message);
            return Result.Failure<Guid>(approvalChainResult.Error);
        }

        var plannedSteps = approvalChainResult.Value;

        // 7. Persist Unified Request
        var newRequest = new Request
        {
            EmployeeId = employee.Id,
            RequestType = request.RequestType,
            Data = jsonData,
            Details = request.Details,
            Status = RequestStatus.Submitted,
            CurrentStepOrder = 1,
            PlannedStepsJson = JsonSerializer.Serialize(plannedSteps),
            CurrentStepApproverIds = plannedSteps.Count > 0
                ? string.Join(",", plannedSteps[0].Approvers.Select(a => a.EmployeeId))
                : null
        };

        await _unitOfWork.Requests.AddAsync(newRequest, cancellationToken);

        // 8. Auto-approve if chain is empty (all steps were skipped due to self-approval prevention)
        if (plannedSteps.Count == 0)
        {
            _logger.LogInformation("Request {RequestId} has empty approval chain - auto-approving as system", newRequest.Id);

            // Auto-approve with system comment
            var history = new RequestApprovalHistory
            {
                RequestId = newRequest.Id,
                ApproverId = employee.Id, // Self-approver when no other approvers available
                Status = RequestStatus.Approved,
                Comment = "Auto accepted by system - no valid approvers in workflow chain"
            };
            newRequest.ApprovalHistory.Add(history);
            newRequest.Status = RequestStatus.Approved;
            newRequest.CurrentStepOrder = 0;

            // Execute final approval strategy
            var finalApprovalStrategy = _strategyFactory.GetStrategy(request.RequestType);
            if (finalApprovalStrategy != null)
            {
                await finalApprovalStrategy.OnFinalApprovalAsync(newRequest, cancellationToken);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Request {RequestId} was auto-approved by system", newRequest.Id);
            return Result.Success(newRequest.Id);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Successfully created {RequestType} request {RequestId} with {StepCount} approval stages.",
            request.RequestType, newRequest.Id, plannedSteps.Count);

        return Result.Success(newRequest.Id);
    }
}
