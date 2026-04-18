using HrSystemApp.Application.DTOs.Requests;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.Errors;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using MediatR;
using Microsoft.Extensions.Logging;

namespace HrSystemApp.Application.Features.Requests.Commands.Admin;

public record UpdateRequestDefinitionCommand : IRequest<Result<Guid>>
{
    public Guid Id { get; set; }
    public bool IsActive { get; set; }
    public List<WorkflowStepDto> Steps { get; set; } = new();
}

public class UpdateRequestDefinitionCommandHandler : IRequestHandler<UpdateRequestDefinitionCommand, Result<Guid>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<UpdateRequestDefinitionCommandHandler> _logger;

    public UpdateRequestDefinitionCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ILogger<UpdateRequestDefinitionCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<Result<Guid>> Handle(UpdateRequestDefinitionCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Attempting to update Request Definition ID {DefinitionId}.", request.Id);

        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return Result.Failure<Guid>(DomainErrors.Auth.Unauthorized);

        var employee = await _unitOfWork.Employees.GetByUserIdAsync(userId, cancellationToken);
        if (employee == null)
            return Result.Failure<Guid>(DomainErrors.Employee.NotFound);

        // 1. Find the definition
        var definition = await _unitOfWork.RequestDefinitions.GetFirstOrDefaultAsync(
            d => d.Id == request.Id,
            cancellationToken,
            d => d.WorkflowSteps
        );

        if (definition == null)
        {
            _logger.LogWarning("UpdateRequestDefinition failed: Definition ID {DefinitionId} not found.", request.Id);
            return Result.Failure<Guid>(DomainErrors.Requests.DefinitionNotFound);
        }

        // 2. Security: Does this user belong to the company and have admin rights?
        if (definition.CompanyId != employee.CompanyId)
        {
            _logger.LogWarning(
                "Unauthorized update attempt for Definition {DefinitionId} by user {UserId} from different company {CompanyId}.",
                request.Id, userId, employee.CompanyId);
            return Result.Failure<Guid>(DomainErrors.Auth.Unauthorized);
        }

        // 3. Validate steps have unique sort orders
        var sortOrders = request.Steps.Select(s => s.SortOrder).ToList();
        if (sortOrders.Distinct().Count() != sortOrders.Count)
        {
            _logger.LogWarning("UpdateRequestDefinition failed: Duplicate sort orders detected.");
            return Result.Failure<Guid>(DomainErrors.General.ArgumentError);
        }

        // 4. Validate each step's referenced entity exists and belongs to this company
        foreach (var step in request.Steps)
        {
            if (step.StepType == WorkflowStepType.OrgNode)
            {
                if (!step.OrgNodeId.HasValue)
                    return Result.Failure<Guid>(DomainErrors.Request.MissingOrgNodeId);

                var node = await _unitOfWork.OrgNodes.GetByIdAsync(step.OrgNodeId.Value, cancellationToken);
                if (node == null)
                    return Result.Failure<Guid>(DomainErrors.OrgNode.NotFound);

                if (node.CompanyId != definition.CompanyId)
                    return Result.Failure<Guid>(DomainErrors.Request.OrgNodeNotInCompany);
            }
            else if (step.StepType == WorkflowStepType.DirectEmployee)
            {
                if (!step.DirectEmployeeId.HasValue)
                    return Result.Failure<Guid>(DomainErrors.Request.MissingDirectEmployeeId);

                var emp = await _unitOfWork.Employees.GetByIdAsync(step.DirectEmployeeId.Value, cancellationToken);
                if (emp == null)
                    return Result.Failure<Guid>(DomainErrors.Employee.NotFound);

                if (emp.CompanyId != definition.CompanyId)
                    return Result.Failure<Guid>(DomainErrors.Request.DirectEmployeeNotInCompany);
            }
        }

        // 5. Cross-step conflict check:
        //    A DirectEmployee approver must not also be a manager at any OrgNode step.
        var directEmployeeIds = request.Steps
            .Where(s => s.StepType == WorkflowStepType.DirectEmployee && s.DirectEmployeeId.HasValue)
            .Select(s => s.DirectEmployeeId!.Value)
            .ToHashSet();

        if (directEmployeeIds.Count > 0)
        {
            var orgNodeSteps = request.Steps.Where(s => s.StepType == WorkflowStepType.OrgNode && s.OrgNodeId.HasValue);
            foreach (var nodeStep in orgNodeSteps)
            {
                var managers = await _unitOfWork.OrgNodeAssignments.GetManagersByNodeAsync(nodeStep.OrgNodeId!.Value, cancellationToken);
                var conflict = managers.Any(m => directEmployeeIds.Contains(m.Id));
                if (conflict)
                    return Result.Failure<Guid>(DomainErrors.Request.DirectEmployeeAlsoNodeManager);
            }
        }

        // 6. Update
        definition.IsActive = request.IsActive;
        definition.WorkflowSteps.Clear();
        definition.WorkflowSteps = request.Steps.Select(s => new RequestWorkflowStep
        {
            StepType = s.StepType,
            OrgNodeId = s.StepType == WorkflowStepType.OrgNode ? s.OrgNodeId : null,
            BypassHierarchyCheck = s.StepType == WorkflowStepType.OrgNode && s.BypassHierarchyCheck,
            DirectEmployeeId = s.StepType == WorkflowStepType.DirectEmployee ? s.DirectEmployeeId : null,
            SortOrder = s.SortOrder,
            RequestDefinitionId = definition.Id
        }).ToList();

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Successfully updated Request Definition ID {DefinitionId} (Type: {Type}). New step count: {StepCount}",
            definition.Id, definition.RequestType, definition.WorkflowSteps.Count);

        return Result.Success(definition.Id);
    }
}
