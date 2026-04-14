using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.OrgNodes;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using MediatR;
using Microsoft.Extensions.Logging;

namespace HrSystemApp.Application.Features.OrgNodes.Commands.CreateOrgNode;

public class CreateOrgNodeCommandHandler : IRequestHandler<CreateOrgNodeCommand, Result<Guid>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<CreateOrgNodeCommandHandler> _logger;

    public CreateOrgNodeCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ILogger<CreateOrgNodeCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<Result<Guid>> Handle(CreateOrgNodeCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Attempting to create OrgNode: {Name}", request.Name);

        // 1. Validate parent exists if provided
        if (request.ParentId.HasValue)
        {
            var parent = await _unitOfWork.OrgNodes.GetByIdAsync(request.ParentId.Value, cancellationToken);
            if (parent == null)
            {
                _logger.LogWarning("CreateOrgNode failed: Parent {ParentId} not found.", request.ParentId);
                return Result.Failure<Guid>(DomainErrors.OrgNode.NotFound);
            }
        }

        // 2. Validate level exists if provided
        if (request.LevelId.HasValue)
        {
            var level = await _unitOfWork.HierarchyLevels.GetByIdAsync(request.LevelId.Value, cancellationToken);
            if (level == null)
            {
                _logger.LogWarning("CreateOrgNode failed: HierarchyLevel {LevelId} not found.", request.LevelId);
                return Result.Failure<Guid>(DomainErrors.HierarchyLevel.NotFound);
            }
        }

        // 3. Validate entity is not already linked to another node
        if (request.EntityId.HasValue && request.EntityType.HasValue)
        {
            var isLinked = await _unitOfWork.OrgNodes.IsLinkedToEntityAsync(
                request.EntityId.Value, request.EntityType.Value, cancellationToken);
            if (isLinked)
            {
                _logger.LogWarning("CreateOrgNode failed: Entity {EntityId} ({EntityType}) is already linked.",
                    request.EntityId, request.EntityType);
                return Result.Failure<Guid>(DomainErrors.OrgNode.DuplicateEntityLink);
            }
        }

        // 4. Create the node
        var node = new OrgNode
        {
            Name = request.Name,
            ParentId = request.ParentId,
            LevelId = request.LevelId,
            EntityId = request.EntityId,
            EntityType = request.EntityType
        };

        await _unitOfWork.OrgNodes.AddAsync(node, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Successfully created OrgNode {NodeId}: {Name}", node.Id, node.Name);
        return Result.Success(node.Id);
    }
}