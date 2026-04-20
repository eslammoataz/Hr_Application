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
        _logger.LogInformation("[RequestCreate] Starting — RequestType={RequestType}", request.RequestType);

        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("[RequestCreate] Unauthorized — no userId");
            return Result.Failure<Guid>(DomainErrors.Auth.Unauthorized);
        }

        var employee = await _unitOfWork.Employees.GetByUserIdAsync(userId, cancellationToken);
        if (employee == null)
        {
            _logger.LogWarning("[RequestCreate] Employee not found — UserId={UserId}", userId);
            return Result.Failure<Guid>(DomainErrors.Employee.NotFound);
        }

        _logger.LogInformation("[RequestCreate] Employee resolved — EmployeeId={EmployeeId}, Name={Name}, CompanyId={CompanyId}",
            employee.Id, employee.FullName, employee.CompanyId);

        var jsonData = request.Data.GetRawText();

        // 1. Resolve Definition
        var definition = await _unitOfWork.RequestDefinitions.GetByTypeAsync(employee.CompanyId, request.RequestType, cancellationToken);
        if (definition == null || !definition.IsActive)
        {
            _logger.LogWarning("[RequestCreate] Definition not found or disabled — CompanyId={CompanyId}, RequestType={RequestType}",
                employee.CompanyId, request.RequestType);
            return Result.Failure<Guid>(DomainErrors.Requests.TypeDisabled);
        }
        _logger.LogInformation("[RequestCreate] Definition found — DefinitionId={DefinitionId}", definition.Id);

        // 2. Structural Schema Validation
        var schemaResult = _schemaValidator.Validate(request.RequestType, jsonData, definition.FormSchemaJson);
        if (!schemaResult.IsSuccess)
        {
            _logger.LogWarning("[RequestCreate] Schema validation failed — DefinitionId={DefinitionId}, Error={Error}",
                definition.Id, schemaResult.Error.Message);
            return Result.Failure<Guid>(schemaResult.Error);
        }

        // 3. Business Logic Validation (Strategy)
        var strategy = _strategyFactory.GetStrategy(request.RequestType);
        if (strategy != null)
        {
            var strategyResult = await strategy.ValidateBusinessRulesAsync(employee.Id, request.Data, cancellationToken);
            if (!strategyResult.IsSuccess)
            {
                _logger.LogWarning("[RequestCreate] Business rules failed — EmployeeId={EmployeeId}, RequestType={RequestType}, Error={Error}",
                    employee.Id, request.RequestType, strategyResult.Error.Message);
                return Result.Failure<Guid>(strategyResult.Error);
            }
        }

        // 4. Get employee's OrgNode assignment
        var assignment = await _unitOfWork.OrgNodeAssignments.GetByEmployeeWithNodeAsync(employee.Id, cancellationToken);
        if (assignment == null)
        {
            _logger.LogWarning("[RequestCreate] No OrgNode assignment — EmployeeId={EmployeeId}", employee.Id);
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
                StartFromLevel = s.StartFromLevel,
                LevelsUp = s.LevelsUp,
                CompanyRoleId = s.CompanyRoleId,
                SortOrder = s.SortOrder
            })
            .ToList();
        _logger.LogInformation("[RequestCreate] Definition steps mapped — Count={Count}", definitionSteps.Count);

        // 5b. Submission-time validation
        foreach (var step in definitionSteps)
        {
            if (step.StepType == WorkflowStepType.OrgNode && step.OrgNodeId.HasValue)
            {
                var node = await _unitOfWork.OrgNodes.GetByIdAsync(step.OrgNodeId.Value, cancellationToken);
                if (node == null || node.CompanyId != employee.CompanyId)
                {
                    _logger.LogWarning("[RequestCreate] OrgNode validation failed — NodeId={NodeId}", step.OrgNodeId);
                    return Result.Failure<Guid>(DomainErrors.Request.OrgNodeNotInCompany);
                }
            }
            else if (step.StepType == WorkflowStepType.DirectEmployee && step.DirectEmployeeId.HasValue)
            {
                var directEmp = await _unitOfWork.Employees.GetByIdAsync(step.DirectEmployeeId.Value, cancellationToken);
                if (directEmp == null || directEmp.CompanyId != employee.CompanyId)
                {
                    _logger.LogWarning("[RequestCreate] DirectEmployee validation failed — DirectEmployeeId={DirectEmployeeId}", step.DirectEmployeeId);
                    return Result.Failure<Guid>(DomainErrors.Request.DirectEmployeeNotInCompany);
                }
                if (directEmp.EmploymentStatus != EmploymentStatus.Active)
                {
                    _logger.LogWarning("[RequestCreate] DirectEmployee not active — DirectEmployeeId={DirectEmployeeId}", step.DirectEmployeeId);
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
                    _logger.LogWarning("[RequestCreate] CompanyRole validation failed — CompanyRoleId={CompanyRoleId}", step.CompanyRoleId);
                    return Result.Failure<Guid>(DomainErrors.Request.RoleNotInCompany);
                }
            }
        }

        // 6. Build approval chain
        _logger.LogInformation("[RequestCreate] Building approval chain — EmployeeId={EmployeeId}, NodeId={NodeId}",
            employee.Id, assignment.OrgNodeId);

        var approvalChainResult = await _workflowResolutionService.BuildApprovalChainAsync(
            employee.Id,
            assignment.OrgNodeId,
            definitionSteps,
            cancellationToken);

        if (!approvalChainResult.IsSuccess)
        {
            _logger.LogError("[RequestCreate] Chain build failed — EmployeeId={EmployeeId}, Error={Error}",
                employee.Id, approvalChainResult.Error.Message);
            return Result.Failure<Guid>(approvalChainResult.Error);
        }

        var plannedSteps = approvalChainResult.Value;
        _logger.LogInformation("[RequestCreate] Chain built — StepsCount={StepsCount}", plannedSteps.Count);

        // 7. Persist Unified Request
        var newRequest = new Request
        {
            EmployeeId = employee.Id,
            RequestType = request.RequestType,
            Data = jsonData,
            Details = request.Details,
            Status = RequestStatus.Submitted,
            CurrentStepOrder = plannedSteps.Count > 0 ? 1 : 0,
            PlannedStepsJson = JsonSerializer.Serialize(plannedSteps),
            CurrentStepApproverIds = plannedSteps.Count > 0
                ? string.Join(",", plannedSteps[0].Approvers.Select(a => a.EmployeeId.ToString()))
                : null
        };

        await _unitOfWork.Requests.AddAsync(newRequest, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("[RequestCreate] Request persisted — RequestId={RequestId}", newRequest.Id);
        return Result.Success(newRequest.Id);
    }
}
