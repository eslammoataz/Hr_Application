using HrSystemApp.Application.Common;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Models;
using MediatR;
using Microsoft.Extensions.Logging;

namespace HrSystemApp.Application.Features.OrgNodes.Commands.DeleteOrgNode;

public class DeleteOrgNodeCommandHandler : IRequestHandler<DeleteOrgNodeCommand, Result<Guid>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<DeleteOrgNodeCommandHandler> _logger;

    public DeleteOrgNodeCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ILogger<DeleteOrgNodeCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<Result<Guid>> Handle(DeleteOrgNodeCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Attempting to delete OrgNode {NodeId}", request.Id);

        var node = await _unitOfWork.OrgNodes.GetByIdAsync(request.Id, cancellationToken);
        if (node == null)
        {
            _logger.LogWarning("DeleteOrgNode failed: Node {NodeId} not found.", request.Id);
            return Result.Failure<Guid>(DomainErrors.OrgNode.NotFound);
        }

        var childCount = await _unitOfWork.OrgNodes.GetChildCountAsync(request.Id, cancellationToken);
        var assignments = await _unitOfWork.OrgNodeAssignments.GetByNodeAsync(request.Id, cancellationToken);
        var hasAssignments = assignments.Count > 0;
        var hasChildren = childCount > 0;

        // Leaf node: no children AND no assignments → HARD DELETE
        if (!hasChildren && !hasAssignments)
        {
            _logger.LogInformation("Leaf node {NodeId}: hard deleting (no children, no assignments)", request.Id);
            await _unitOfWork.OrgNodes.DeleteAsync(node, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Successfully hard deleted OrgNode {NodeId}", request.Id);
            return Result.Success(request.Id);
        }

        // Has children or assignments → Reparent + SOFT DELETE
        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var parentId = node.ParentId;

            // Move children to parent (or to null if root)
            if (hasChildren)
            {
                var children = await _unitOfWork.OrgNodes.GetChildrenAsync(node.Id, cancellationToken);
                foreach (var child in children)
                {
                    child.ParentId = parentId;
                    await _unitOfWork.OrgNodes.UpdateAsync(child, cancellationToken);
                }
                _logger.LogInformation("Reparented {ChildCount} children to {ParentId}",
                    children.Count, parentId ?? (object)"root");
            }

            // Soft delete all assignments
            if (hasAssignments)
            {
                foreach (var assignment in assignments)
                {
                    assignment.IsDeleted = true;
                    await _unitOfWork.OrgNodeAssignments.UpdateAsync(assignment, cancellationToken);
                }
            }

            // Soft delete the node
            await _unitOfWork.OrgNodes.DeleteAsync(node, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _unitOfWork.CommitTransactionAsync(cancellationToken);
            _logger.LogInformation("Successfully soft deleted OrgNode {NodeId} (reparented {ChildCount} children)",
                request.Id, childCount);
            return Result.Success(request.Id);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            _logger.LogError(ex, "Failed to delete OrgNode {NodeId}", request.Id);
            throw;
        }
    }
}