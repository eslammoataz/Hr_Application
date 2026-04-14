using HrSystemApp.Application.Common;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Models;
using MediatR;
using Microsoft.Extensions.Logging;

namespace HrSystemApp.Application.Features.OrgNodes.Commands.UpdateOrgNode;

public class UpdateOrgNodeCommandHandler : IRequestHandler<UpdateOrgNodeCommand, Result<Guid>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<UpdateOrgNodeCommandHandler> _logger;

    public UpdateOrgNodeCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ILogger<UpdateOrgNodeCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<Result<Guid>> Handle(UpdateOrgNodeCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Attempting to update OrgNode {NodeId}", request.Id);

        var node = await _unitOfWork.OrgNodes.GetByIdAsync(request.Id, cancellationToken);
        if (node == null)
        {
            _logger.LogWarning("UpdateOrgNode failed: Node {NodeId} not found.", request.Id);
            return Result.Failure<Guid>(DomainErrors.OrgNode.NotFound);
        }

        // Validate new parent if provided
        if (request.ParentId.HasValue)
        {
            // Cannot set itself as parent
            if (request.ParentId.Value == request.Id)
            {
                _logger.LogWarning("UpdateOrgNode failed: Node {NodeId} cannot be its own parent.", request.Id);
                return Result.Failure<Guid>(DomainErrors.OrgNode.CircularReference);
            }

            // Validate new parent exists
            var parent = await _unitOfWork.OrgNodes.GetByIdAsync(request.ParentId.Value, cancellationToken);
            if (parent == null)
            {
                _logger.LogWarning("UpdateOrgNode failed: Parent {ParentId} not found.", request.ParentId);
                return Result.Failure<Guid>(DomainErrors.OrgNode.NotFound);
            }

            // Check for circular reference: new parent cannot be a descendant of this node
            var isDescendant = await _unitOfWork.OrgNodes.IsAncestorOfAsync(request.Id, request.ParentId.Value, cancellationToken);
            if (isDescendant)
            {
                _logger.LogWarning("UpdateOrgNode failed: Circular reference detected.");
                return Result.Failure<Guid>(DomainErrors.OrgNode.CircularReference);
            }
        }

        // Validate level exists if provided
        if (request.LevelId.HasValue)
        {
            var level = await _unitOfWork.HierarchyLevels.GetByIdAsync(request.LevelId.Value, cancellationToken);
            if (level == null)
            {
                _logger.LogWarning("UpdateOrgNode failed: Level {LevelId} not found.", request.LevelId);
                return Result.Failure<Guid>(DomainErrors.HierarchyLevel.NotFound);
            }
        }

        // Update the node
        node.Name = request.Name;
        node.ParentId = request.ParentId;
        node.LevelId = request.LevelId;

        await _unitOfWork.OrgNodes.UpdateAsync(node, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Successfully updated OrgNode {NodeId}", node.Id);
        return Result.Success(node.Id);
    }
}