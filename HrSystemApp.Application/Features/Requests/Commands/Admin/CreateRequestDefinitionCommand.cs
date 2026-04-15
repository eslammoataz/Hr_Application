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

public record CreateRequestDefinitionCommand : IRequest<Result<Guid>>
{
    public Guid? CompanyId { get; set; }
    public RequestType RequestType { get; set; }
    public List<WorkflowStepDto> Steps { get; set; } = new();
}

public class CreateRequestDefinitionCommandHandler : IRequestHandler<CreateRequestDefinitionCommand, Result<Guid>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<CreateRequestDefinitionCommandHandler> _logger;

    public CreateRequestDefinitionCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ILogger<CreateRequestDefinitionCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<Result<Guid>> Handle(CreateRequestDefinitionCommand request, CancellationToken cancellationToken)
    {
        Guid targetCompanyId;

        if (_currentUserService.Role == nameof(UserRole.SuperAdmin))
        {
            if (!request.CompanyId.HasValue || request.CompanyId.Value == Guid.Empty)
                return Result.Failure<Guid>(DomainErrors.General.ArgumentError);

            targetCompanyId = request.CompanyId.Value;
        }
        else
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
                return Result.Failure<Guid>(DomainErrors.Auth.Unauthorized);

            var employee = await _unitOfWork.Employees.GetByUserIdAsync(userId, cancellationToken);
            if (employee == null)
                return Result.Failure<Guid>(DomainErrors.Employee.NotFound);

            targetCompanyId = employee.CompanyId;
        }

        _logger.LogInformation("Attempting to create Request Definition for Company {CompanyId} (Type: {Type}).",
            targetCompanyId, request.RequestType);

        // 1. Check if already exists
        var existing = await _unitOfWork.RequestDefinitions.GetByTypeAsync(targetCompanyId, request.RequestType, cancellationToken);
        if (existing != null)
        {
            _logger.LogWarning("CreateRequestDefinition failed: Definition already exists for Company {CompanyId}, Type {Type}.",
                targetCompanyId, request.RequestType);
            return Result.Failure<Guid>(DomainErrors.Requests.DefinitionNotFound);
        }

        // 2. Validate steps have unique sort orders
        var sortOrders = request.Steps.Select(s => s.SortOrder).ToList();
        if (sortOrders.Distinct().Count() != sortOrders.Count)
        {
            _logger.LogWarning("CreateRequestDefinition failed: Duplicate sort orders detected.");
            return Result.Failure<Guid>(DomainErrors.General.ArgumentError);
        }

        // 3. Validate each OrgNode exists
        foreach (var step in request.Steps)
        {
            var node = await _unitOfWork.OrgNodes.GetByIdAsync(step.OrgNodeId, cancellationToken);
            if (node == null)
            {
                _logger.LogWarning("CreateRequestDefinition failed: OrgNode {NodeId} not found.", step.OrgNodeId);
                return Result.Failure<Guid>(DomainErrors.OrgNode.NotFound);
            }
        }

        // 4. Create Definition
        var definition = new RequestDefinition
        {
            CompanyId = targetCompanyId,
            RequestType = request.RequestType,
            IsActive = true,
            WorkflowSteps = request.Steps.Select(s => new RequestWorkflowStep
            {
                OrgNodeId = s.OrgNodeId,
                SortOrder = s.SortOrder
            }).ToList()
        };

        await _unitOfWork.RequestDefinitions.AddAsync(definition, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Successfully created Request Definition ID {DefinitionId} for Company {CompanyId} (Type: {Type}) with {StepCount} logic steps.",
            definition.Id, definition.CompanyId, definition.RequestType, definition.WorkflowSteps.Count);

        return Result.Success(definition.Id);
    }
}
