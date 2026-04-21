using System.Diagnostics;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.OrgNodes;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Models;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using HrSystemApp.Application.Common.Logging;

namespace HrSystemApp.Application.Features.OrgNodes.Commands.UpdateOrgNode;

public class UpdateOrgNodeCommandHandler : IRequestHandler<UpdateOrgNodeCommand, Result<Guid>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<UpdateOrgNodeCommandHandler> _logger;
    private readonly LoggingOptions _loggingOptions;

    public UpdateOrgNodeCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ILogger<UpdateOrgNodeCommandHandler> logger,
        IOptions<LoggingOptions> loggingOptions)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _logger = logger;
        _loggingOptions = loggingOptions.Value;
    }

    public async Task<Result<Guid>> Handle(UpdateOrgNodeCommand request, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        _logger.LogActionStart(_loggingOptions, LogAction.OrgNode.UpdateOrgNode);

        var node = await _unitOfWork.OrgNodes.GetByIdAsync(request.Id, cancellationToken);
        if (node == null)
        {
            _logger.LogDecision(_loggingOptions, LogAction.OrgNode.UpdateOrgNode, LogStage.Processing,
                "NodeNotFound", new { NodeId = request.Id });
            sw.Stop();
            return Result.Failure<Guid>(DomainErrors.OrgNode.NotFound);
        }

        if (request.ParentId.HasValue)
        {
            if (request.ParentId.Value == request.Id)
            {
                _logger.LogDecision(_loggingOptions, LogAction.OrgNode.UpdateOrgNode, LogStage.Processing,
                    "CircularReference", new { NodeId = request.Id });
                sw.Stop();
                return Result.Failure<Guid>(DomainErrors.OrgNode.CircularReference);
            }

            var parent = await _unitOfWork.OrgNodes.GetByIdAsync(request.ParentId.Value, cancellationToken);
            if (parent == null)
            {
                _logger.LogDecision(_loggingOptions, LogAction.OrgNode.UpdateOrgNode, LogStage.Processing,
                    "ParentNotFound", new { ParentId = request.ParentId });
                sw.Stop();
                return Result.Failure<Guid>(DomainErrors.OrgNode.NotFound);
            }

            var isDescendant = await _unitOfWork.OrgNodes.IsAncestorOfAsync(request.Id, request.ParentId.Value, cancellationToken);
            if (isDescendant)
            {
                _logger.LogDecision(_loggingOptions, LogAction.OrgNode.UpdateOrgNode, LogStage.Processing,
                    "CircularReference", new { NodeId = request.Id, ParentId = request.ParentId });
                sw.Stop();
                return Result.Failure<Guid>(DomainErrors.OrgNode.CircularReference);
            }
        }

        node.Name = request.Name;
        node.ParentId = request.ParentId;
        node.Type = request.Type?.Trim().ToLower();

        await _unitOfWork.OrgNodes.UpdateAsync(node, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        sw.Stop();
        _logger.LogActionSuccess(_loggingOptions, LogAction.OrgNode.UpdateOrgNode, sw.ElapsedMilliseconds);

        return Result.Success(node.Id);
    }
}