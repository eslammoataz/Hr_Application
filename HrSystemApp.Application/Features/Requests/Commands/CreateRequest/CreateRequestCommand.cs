using System.Text.Json;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.Common.Logging;
using HrSystemApp.Application.DTOs.Requests;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Features.Requests.Strategies;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
    private readonly LoggingOptions _loggingOptions;

    public CreateRequestCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IWorkflowResolutionService workflowResolutionService,
        IRequestSchemaValidator schemaValidator,
        IRequestStrategyFactory strategyFactory,
        ILogger<CreateRequestCommandHandler> logger,
        IOptions<LoggingOptions> loggingOptions)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _workflowResolutionService = workflowResolutionService;
        _schemaValidator = schemaValidator;
        _strategyFactory = strategyFactory;
        _logger = logger;
        _loggingOptions = loggingOptions.Value;
    }

    public async Task<Result<Guid>> Handle(CreateRequestCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarningUnauthorized(_loggingOptions, LogAction.Workflow.CreateRequest);
            return Result.Failure<Guid>(DomainErrors.Auth.Unauthorized);
        }

        var employee = await _unitOfWork.Employees.GetByUserIdAsync(userId, cancellationToken);
        if (employee == null)
        {
            _logger.LogWarningUnauthorized(_loggingOptions, LogAction.Workflow.CreateRequest);
            return Result.Failure<Guid>(DomainErrors.Employee.NotFound);
        }

        _logger.LogBusinessFlow(_loggingOptions, LogAction.Workflow.CreateRequest, LogStage.Authorization,
            "EmployeeFound", new { EmployeeId = employee.Id });

        var jsonData = request.Data.GetRawText();

        var definition = await _unitOfWork.RequestDefinitions.GetByTypeAsync(employee.CompanyId, request.RequestType, cancellationToken);
        if (definition == null || !definition.IsActive)
        {
            _logger.LogDecision(_loggingOptions, LogAction.Workflow.CreateRequest, LogStage.Validation,
                "DefinitionMissing", new { request.RequestType, CompanyId = employee.CompanyId });
            return Result.Failure<Guid>(DomainErrors.Requests.TypeDisabled);
        }

        var schemaResult = _schemaValidator.Validate(request.RequestType, jsonData, definition.FormSchemaJson);
        if (!schemaResult.IsSuccess)
        {
            _logger.LogDecision(_loggingOptions, LogAction.Workflow.CreateRequest, LogStage.Validation,
                "SchemaValidationFailed", new { DefinitionId = definition.Id });
            return Result.Failure<Guid>(schemaResult.Error);
        }

        _logger.LogDecision(_loggingOptions, LogAction.Workflow.CreateRequest, LogStage.Validation,
            "SchemaValidationPassed", new { DefinitionId = definition.Id });

        var strategy = _strategyFactory.GetStrategy(request.RequestType);
        if (strategy != null)
        {
            var strategyResult = await strategy.ValidateBusinessRulesAsync(employee.Id, request.Data, cancellationToken);
            if (!strategyResult.IsSuccess)
            {
                _logger.LogDecision(_loggingOptions, LogAction.Workflow.CreateRequest, LogStage.Validation,
                    "BusinessRulesFailed", new { EmployeeId = employee.Id, RequestType = request.RequestType.ToString() });
                return Result.Failure<Guid>(strategyResult.Error);
            }
        }

        _logger.LogDecision(_loggingOptions, LogAction.Workflow.CreateRequest, LogStage.Validation,
            "BusinessRulesPassed", new { RequestType = request.RequestType.ToString() });

        var assignment = await _unitOfWork.OrgNodeAssignments.GetByEmployeeWithNodeAsync(employee.Id, cancellationToken);
        if (assignment == null)
        {
            _logger.LogDecision(_loggingOptions, LogAction.Workflow.CreateRequest, LogStage.Validation,
                "NoOrgNodeAssignment", new { EmployeeId = employee.Id });
            return Result.Failure<Guid>(DomainErrors.OrgNode.NotFound);
        }

        var definitionSteps = definition.WorkflowSteps
            .Select(s => new WorkflowStepDto
            {
                StepType = s.StepType,
                OrgNodeId = s.OrgNodeId,
                BypassHierarchyCheck = s.BypassHierarchyCheck,
                DirectEmployeeId = s.DirectEmployeeId,
                StartFromLevel = s.StartFromLevel,
                LevelsUp = s.LevelsUp,
                CompanyRoleId = s.CompanyRoleId,
                SortOrder = s.SortOrder
            })
            .ToList();

        _logger.LogDecision(_loggingOptions, LogAction.Workflow.CreateRequest, LogStage.Processing,
            "DefinitionStepsMapped", new { StepCount = definitionSteps.Count });

        foreach (var step in definitionSteps)
        {
            if (step.StepType == WorkflowStepType.OrgNode && step.OrgNodeId.HasValue)
            {
                var node = await _unitOfWork.OrgNodes.GetByIdAsync(step.OrgNodeId.Value, cancellationToken);
                if (node == null || node.CompanyId != employee.CompanyId)
                {
                    _logger.LogDecision(_loggingOptions, LogAction.Workflow.CreateRequest, LogStage.Validation,
                        "OrgNodeInvalid", new { NodeId = step.OrgNodeId });
                    return Result.Failure<Guid>(DomainErrors.Request.OrgNodeNotInCompany);
                }
            }
            else if (step.StepType == WorkflowStepType.DirectEmployee && step.DirectEmployeeId.HasValue)
            {
                var directEmp = await _unitOfWork.Employees.GetByIdAsync(step.DirectEmployeeId.Value, cancellationToken);
                if (directEmp == null || directEmp.CompanyId != employee.CompanyId)
                {
                    _logger.LogDecision(_loggingOptions, LogAction.Workflow.CreateRequest, LogStage.Validation,
                        "DirectEmployeeInvalid", new { DirectEmployeeId = step.DirectEmployeeId });
                    return Result.Failure<Guid>(DomainErrors.Request.DirectEmployeeNotInCompany);
                }
                if (directEmp.EmploymentStatus != EmploymentStatus.Active)
                {
                    _logger.LogDecision(_loggingOptions, LogAction.Workflow.CreateRequest, LogStage.Validation,
                        "DirectEmployeeNotActive", new { DirectEmployeeId = step.DirectEmployeeId });
                    return Result.Failure<Guid>(DomainErrors.Request.DirectEmployeeNotActive);
                }
            }
            else if (step.StepType == WorkflowStepType.CompanyRole)
            {
                if (!step.CompanyRoleId.HasValue)
                    return Result.Failure<Guid>(DomainErrors.Request.MissingCompanyRoleId);

                var role = await _unitOfWork.CompanyRoles.GetByIdAsync(step.CompanyRoleId.Value, cancellationToken);
                if (role is null || role.IsDeleted || role.CompanyId != employee.CompanyId)
                {
                    _logger.LogDecision(_loggingOptions, LogAction.Workflow.CreateRequest, LogStage.Validation,
                        "CompanyRoleInvalid", new { CompanyRoleId = step.CompanyRoleId });
                    return Result.Failure<Guid>(DomainErrors.Request.RoleNotInCompany);
                }
            }
        }

        _logger.LogDecision(_loggingOptions, LogAction.Workflow.CreateRequest, LogStage.Processing,
            "StepValidationPassed", new { EmployeeId = employee.Id, NodeId = assignment.OrgNodeId });

        var approvalChainResult = await _workflowResolutionService.BuildApprovalChainAsync(
            employee.Id,
            assignment.OrgNodeId,
            definitionSteps,
            cancellationToken);

        if (!approvalChainResult.IsSuccess)
        {
            _logger.LogDecision(_loggingOptions, LogAction.Workflow.CreateRequest, LogStage.Processing,
                "ChainBuildFailed", new { EmployeeId = employee.Id, Error = approvalChainResult.Error.Message });
            return Result.Failure<Guid>(approvalChainResult.Error);
        }

        var plannedSteps = approvalChainResult.Value;

        _logger.LogBusinessFlow(_loggingOptions, LogAction.Workflow.CreateRequest, LogStage.Processing,
            "PlannedStepsResolved", new { StepCount = plannedSteps.Count });

        bool isEmptyChain = plannedSteps.Count == 0;
        var requestStatus = isEmptyChain ? RequestStatus.Approved : RequestStatus.Submitted;
        var currentStepOrder = isEmptyChain ? 0 : 1;

        var newRequest = new Request
        {
            EmployeeId = employee.Id,
            RequestType = request.RequestType,
            Status = requestStatus,
            Data = jsonData,
            Details = request.Details,
            PlannedStepsJson = JsonSerializer.Serialize(plannedSteps),
            CurrentStepOrder = currentStepOrder,
            CurrentStepApproverIds = !isEmptyChain && plannedSteps.Count > 0
                ? string.Join(",", plannedSteps[0].Approvers.Select(a => a.EmployeeId.ToString()))
                : null
        };

        if (isEmptyChain)
        {
            _logger.LogDecision(_loggingOptions, LogAction.Workflow.CreateRequest, LogStage.Processing,
                "EmptyChainAutoApproved", new { EmployeeId = employee.Id, RequestId = newRequest.Id });

            newRequest.ApprovalHistory.Add(new RequestApprovalHistory
            {
                RequestId = newRequest.Id,
                ApproverId = employee.Id,
                Status = RequestStatus.Approved,
                Comment = "Auto-approved: workflow chain produced no approvers"
            });
        }

        await _unitOfWork.Requests.AddAsync(newRequest, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(newRequest.Id);
    }
}