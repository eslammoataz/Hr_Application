using HrSystemApp.Application.Common;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Models;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using HrSystemApp.Application.Common.Logging;

namespace HrSystemApp.Application.Features.OrgNodes.Commands.DeleteOrgNode;

public class DeleteOrgNodeCommandHandler : IRequestHandler<DeleteOrgNodeCommand, Result<Guid>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<DeleteOrgNodeCommandHandler> _logger;
    private readonly LoggingOptions _loggingOptions;

    public DeleteOrgNodeCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ILogger<DeleteOrgNodeCommandHandler> logger,
        IOptions<LoggingOptions> loggingOptions)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _logger = logger;
        _loggingOptions = loggingOptions.Value;
    }

    public async Task<Result<Guid>> Handle(DeleteOrgNodeCommand request, CancellationToken cancellationToken)
    {

        var node = await _unitOfWork.OrgNodes.GetByIdAsync(request.Id, cancellationToken);
        if (node == null)
        {
            _logger.LogDecision(_loggingOptions, LogAction.OrgNode.DeleteOrgNode, LogStage.Processing,
                "NodeNotFound", new { NodeId = request.Id });
            return Result.Failure<Guid>(DomainErrors.OrgNode.NotFound);
        }

        var childCount = await _unitOfWork.OrgNodes.GetChildCountAsync(request.Id, cancellationToken);
        var assignments = await _unitOfWork.OrgNodeAssignments.GetByNodeAsync(request.Id, cancellationToken);
        var hasAssignments = assignments.Count > 0;
        var hasChildren = childCount > 0;

        if (!hasChildren && !hasAssignments)
        {
            await _unitOfWork.OrgNodes.DeleteAsync(node, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success(request.Id);
        }

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var parentId = node.ParentId;

            if (hasChildren)
            {
                var children = await _unitOfWork.OrgNodes.GetChildrenAsync(node.Id, cancellationToken);
                foreach (var child in children)
                    child.ParentId = parentId;
            }

            if (hasAssignments)
            {
                foreach (var assignment in assignments)
                    assignment.IsDeleted = true;
            }

            await _unitOfWork.OrgNodes.DeleteAsync(node, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _unitOfWork.CommitTransactionAsync(cancellationToken);
            return Result.Success(request.Id);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            _logger.LogActionFailure(_loggingOptions, LogAction.OrgNode.DeleteOrgNode, LogStage.Processing, ex,
                new { NodeId = request.Id });
            throw;
        }
    }
}