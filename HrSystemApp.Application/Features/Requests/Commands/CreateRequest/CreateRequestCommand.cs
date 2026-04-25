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

public record CreateRequestCommand(Guid RequestTypeId, JsonElement Data, string? Details = null) : IRequest<Result<Guid>>;

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
            "EmployeeFound", new { EmployeeId = employee.Id, CompanyId = employee.CompanyId, NodeAssignmentExists = false });

        // Look up the RequestType entity
        var requestType = await _unitOfWork.RequestTypes.GetByIdAsync(request.RequestTypeId, cancellationToken);
        if (requestType == null)
        {
            _logger.LogDecision(_loggingOptions, LogAction.Workflow.CreateRequest, LogStage.Validation,
                "RequestTypeNotFound", new { RequestTypeId = request.RequestTypeId });
            return Result.Failure<Guid>(DomainErrors.Requests.TypeDisabled);
        }

        _logger.LogBusinessFlow(_loggingOptions, LogAction.Workflow.CreateRequest, LogStage.Validation,
            "RequestTypeFound", new { RequestTypeId = request.RequestTypeId, KeyName = requestType.KeyName, IsSystemType = requestType.IsSystemType });

        var jsonData = request.Data.GetRawText();

        var definition = await _unitOfWork.RequestDefinitions.GetByTypeAsync(employee.CompanyId, request.RequestTypeId, cancellationToken);
        if (definition == null || !definition.IsActive)
        {
            _logger.LogDecision(_loggingOptions, LogAction.Workflow.CreateRequest, LogStage.Validation,
                "DefinitionMissing", new { RequestTypeId = request.RequestTypeId, CompanyId = employee.CompanyId, IsActive = definition?.IsActive });
            return Result.Failure<Guid>(DomainErrors.Requests.TypeDisabled);
        }

        _logger.LogBusinessFlow(_loggingOptions, LogAction.Workflow.CreateRequest, LogStage.Validation,
            "DefinitionFound", new { DefinitionId = definition.Id, StepCount = definition.WorkflowSteps.Count });

        var schemaResult = _schemaValidator.Validate(requestType.KeyName, jsonData, definition.FormSchemaJson);
        if (!schemaResult.IsSuccess)
        {
            _logger.LogDecision(_loggingOptions, LogAction.Workflow.CreateRequest, LogStage.Validation,
                "SchemaValidationFailed", new { DefinitionId = definition.Id, Error = schemaResult.Error.Message });
            return Result.Failure<Guid>(schemaResult.Error);
        }

        _logger.LogDecision(_loggingOptions, LogAction.Workflow.CreateRequest, LogStage.Validation,
            "SchemaValidationPassed", new { DefinitionId = definition.Id });

        // Resolve strategy by KeyName (e.g., "Leave", "Permission", etc.)
        var strategy = _strategyFactory.GetStrategy(requestType.KeyName);
        if (strategy != null)
        {
            _logger.LogDecision(_loggingOptions, LogAction.Workflow.CreateRequest, LogStage.Validation,
                "StrategyResolved", new { StrategyType = strategy.GetType().Name });
            var strategyResult = await strategy.ValidateBusinessRulesAsync(employee.Id, request.RequestTypeId, request.Data, cancellationToken);
            if (!strategyResult.IsSuccess)
            {
                _logger.LogDecision(_loggingOptions, LogAction.Workflow.CreateRequest, LogStage.Validation,
                    "BusinessRulesFailed", new { EmployeeId = employee.Id, RequestTypeKey = requestType.KeyName });
                return Result.Failure<Guid>(strategyResult.Error);
            }
        }

        _logger.LogBusinessFlow(_loggingOptions, LogAction.Workflow.CreateRequest, LogStage.Validation,
            "BusinessRulesPassed", new { RequestTypeKey = requestType.KeyName });

        var assignment = await _unitOfWork.OrgNodeAssignments.GetByEmployeeWithNodeAsync(employee.Id, cancellationToken);
        if (assignment == null)
        {
            _logger.LogDecision(_loggingOptions, LogAction.Workflow.CreateRequest, LogStage.Validation,
                "NoOrgNodeAssignment", new { EmployeeId = employee.Id });
            return Result.Failure<Guid>(DomainErrors.OrgNode.NotFound);
        }

        _logger.LogBusinessFlow(_loggingOptions, LogAction.Workflow.CreateRequest, LogStage.Validation,
            "OrgNodeAssignmentFound", new { EmployeeId = employee.Id, OrgNodeId = assignment.OrgNodeId });

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

        _logger.LogBusinessFlow(_loggingOptions, LogAction.Workflow.CreateRequest, LogStage.Processing,
            "DefinitionStepsMapped", new { StepCount = definitionSteps.Count });

        var orgNodeIds = definitionSteps
            .Where(s => s.StepType == WorkflowStepType.OrgNode && s.OrgNodeId.HasValue)
            .Select(s => s.OrgNodeId!.Value)
            .Distinct()
            .ToList();
        var directEmployeeIds = definitionSteps
            .Where(s => s.StepType == WorkflowStepType.DirectEmployee && s.DirectEmployeeId.HasValue)
            .Select(s => s.DirectEmployeeId!.Value)
            .Distinct()
            .ToList();
        var companyRoleIds = definitionSteps
            .Where(s => s.StepType == WorkflowStepType.CompanyRole && s.CompanyRoleId.HasValue)
            .Select(s => s.CompanyRoleId!.Value)
            .Distinct()
            .ToList();

        _logger.LogDecision(_loggingOptions, LogAction.Workflow.CreateRequest, LogStage.Processing,
            "FetchingEntities", new { OrgNodeIds = orgNodeIds, DirectEmployeeIds = directEmployeeIds, CompanyRoleIds = companyRoleIds });

        var orgNodes = orgNodeIds.Count > 0
       ? await _unitOfWork.OrgNodes.GetByIdsAsync(orgNodeIds, cancellationToken)
       : new Dictionary<Guid, OrgNode>();

        var employees = directEmployeeIds.Count > 0
            ? await _unitOfWork.Employees.GetByIdsAsync(directEmployeeIds, cancellationToken)
            : new Dictionary<Guid, Employee>();

        var roles = companyRoleIds.Count > 0
            ? await _unitOfWork.CompanyRoles.GetByIdsAsync(companyRoleIds, cancellationToken)
            : new Dictionary<Guid, CompanyRole>();

        _logger.LogDecision(_loggingOptions, LogAction.Workflow.CreateRequest, LogStage.Processing,
            "EntitiesLoaded", new { OrgNodesFound = orgNodes.Count, EmployeesFound = employees.Count, RolesFound = roles.Count });

        foreach (var step in definitionSteps)
        {
            if (step.StepType == WorkflowStepType.OrgNode && step.OrgNodeId.HasValue)
            {
                if (!orgNodes.TryGetValue(step.OrgNodeId.Value, out var node) || node.CompanyId != employee.CompanyId)
                {
                    _logger.LogDecision(_loggingOptions, LogAction.Workflow.CreateRequest, LogStage.Validation,
                        "OrgNodeInvalid", new { NodeId = step.OrgNodeId, Found = orgNodes.ContainsKey(step.OrgNodeId.Value), CompanyMatch = node?.CompanyId == employee.CompanyId });
                    return Result.Failure<Guid>(DomainErrors.Request.OrgNodeNotInCompany);
                }
            }
            else if (step.StepType == WorkflowStepType.DirectEmployee && step.DirectEmployeeId.HasValue)
            {
                if (!employees.TryGetValue(step.DirectEmployeeId.Value, out var directEmp) || directEmp.CompanyId != employee.CompanyId)
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
                {
                    _logger.LogDecision(_loggingOptions, LogAction.Workflow.CreateRequest, LogStage.Validation,
                        "CompanyRoleIdMissing", new { StepType = "CompanyRole" });
                    return Result.Failure<Guid>(DomainErrors.Request.MissingCompanyRoleId);
                }

                if (!roles.TryGetValue(step.CompanyRoleId.Value, out var role))
                {
                    _logger.LogDecision(_loggingOptions, LogAction.Workflow.CreateRequest, LogStage.Validation,
                        "CompanyRoleNotFound", new { CompanyRoleId = step.CompanyRoleId });
                    return Result.Failure<Guid>(DomainErrors.Request.RoleNotFound);
                }

                if (role.IsDeleted)
                {
                    _logger.LogDecision(_loggingOptions, LogAction.Workflow.CreateRequest, LogStage.Validation,
                        "CompanyRoleDeleted", new { CompanyRoleId = step.CompanyRoleId });
                    return Result.Failure<Guid>(DomainErrors.Request.RoleNotInCompany);
                }

                if (role.CompanyId != employee.CompanyId)
                {
                    _logger.LogDecision(_loggingOptions, LogAction.Workflow.CreateRequest, LogStage.Validation,
                        "CompanyRoleWrongCompany", new { CompanyRoleId = step.CompanyRoleId, RoleCompanyId = role.CompanyId, EmployeeCompanyId = employee.CompanyId });
                    return Result.Failure<Guid>(DomainErrors.Request.RoleNotInCompany);
                }

                _logger.LogDecision(_loggingOptions, LogAction.Workflow.CreateRequest, LogStage.Validation,
                    "CompanyRoleValid", new { CompanyRoleId = step.CompanyRoleId, RoleName = role.Name, IsDeleted = role.IsDeleted });
            }
        }

        _logger.LogDecision(_loggingOptions, LogAction.Workflow.CreateRequest, LogStage.Processing,
            "StepValidationPassed", new { EmployeeId = employee.Id, NodeId = assignment.OrgNodeId });

        _logger.LogDecision(_loggingOptions, LogAction.Workflow.CreateRequest, LogStage.Processing,
            "BuildingApprovalChain_START", new { RequesterEmployeeId = employee.Id, RequesterNodeId = assignment.OrgNodeId, StepsToResolve = definitionSteps.Count, Steps = definitionSteps.Select(s => new { s.StepType, s.CompanyRoleId, s.OrgNodeId, s.DirectEmployeeId, s.SortOrder }).ToList() });

        var approvalChainResult = await _workflowResolutionService.BuildApprovalChainAsync(
            employee.Id,
            assignment.OrgNodeId,
            definitionSteps,
            cancellationToken);

        _logger.LogDecision(_loggingOptions, LogAction.Workflow.CreateRequest, LogStage.Processing,
            "BuildApprovalChain_RETURNED", new { IsSuccess = approvalChainResult.IsSuccess, StepCount = approvalChainResult.Value?.Count ?? -1, Steps = approvalChainResult.Value?.Select(s => new { s.StepType, s.NodeName, s.CompanyRoleId, ApproverCount = s.Approvers.Count, Approvers = s.Approvers.Select(a => new { a.EmployeeId, a.EmployeeName }).ToList() }).ToList() });

        if (!approvalChainResult.IsSuccess)
        {
            _logger.LogDecision(_loggingOptions, LogAction.Workflow.CreateRequest, LogStage.Processing,
                "ChainBuildFailed", new { EmployeeId = employee.Id, Error = approvalChainResult.Error.Message });
            return Result.Failure<Guid>(approvalChainResult.Error);
        }

        var plannedSteps = approvalChainResult.Value;

        _logger.LogBusinessFlow(_loggingOptions, LogAction.Workflow.CreateRequest, LogStage.Processing,
            "PlannedStepsResolved", new { StepCount = plannedSteps.Count, Steps = plannedSteps.Select(s => new { s.StepType, s.NodeName, ApproverCount = s.Approvers.Count, Approvers = s.Approvers.Select(a => new { a.EmployeeId, a.EmployeeName }) }) });

        bool isEmptyChain = plannedSteps.Count == 0;
        var requestStatus = isEmptyChain ? RequestStatus.Approved : RequestStatus.Submitted;
        var currentStepOrder = isEmptyChain ? 0 : 1;

        _logger.LogDecision(_loggingOptions, LogAction.Workflow.CreateRequest, LogStage.Processing,
            "RequestStatusDetermined", new { IsEmptyChain = isEmptyChain, RequestStatus = requestStatus.ToString(), CurrentStepOrder = currentStepOrder });

        var newRequest = new Request
        {
            Id = Guid.NewGuid(),
            EmployeeId = employee.Id,
            RequestTypeId = request.RequestTypeId,
            Status = requestStatus,
            DynamicDataJson = jsonData,
            Details = request.Details,
            PlannedStepsJson = JsonSerializer.Serialize(plannedSteps),
            CurrentStepOrder = currentStepOrder,
            CurrentStepApproverIds = !isEmptyChain && plannedSteps.Count > 0
                ? string.Join(",", plannedSteps[0].Approvers.Select(a => a.EmployeeId.ToString()))
                : null,
            CapturedSchemaJson = requestType.FormSchemaJson,
            DueDate = requestType.DefaultSlaDays.HasValue
                ? DateTime.UtcNow.AddDays(requestType.DefaultSlaDays.Value)
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

        _logger.LogBusinessFlow(_loggingOptions, LogAction.Workflow.CreateRequest, LogStage.Finalization,
            "RequestCreated", new { RequestId = newRequest.Id, Status = newRequest.Status.ToString(), StepCount = plannedSteps.Count });

        return Result.Success(newRequest.Id);
    }
}