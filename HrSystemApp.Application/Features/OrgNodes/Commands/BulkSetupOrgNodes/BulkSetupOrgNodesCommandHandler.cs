using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.OrgNodes;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Domain.Models;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using HrSystemApp.Application.Common.Logging;

namespace HrSystemApp.Application.Features.OrgNodes.Commands.BulkSetupOrgNodes;

public class BulkSetupOrgNodesCommandHandler : IRequestHandler<BulkSetupOrgNodesCommand, Result<BulkSetupOrgNodesResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<BulkSetupOrgNodesCommandHandler> _logger;
    private readonly LoggingOptions _loggingOptions;

    public BulkSetupOrgNodesCommandHandler(
        IUnitOfWork unitOfWork,
        ILogger<BulkSetupOrgNodesCommandHandler> logger,
        IOptions<LoggingOptions> loggingOptions)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _loggingOptions = loggingOptions.Value;
    }

    public async Task<Result<BulkSetupOrgNodesResponse>> Handle(BulkSetupOrgNodesCommand request, CancellationToken cancellationToken)
    {

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var tempIdToNode = new Dictionary<string, OrgNode>();
            var tempIdToDepth = new Dictionary<string, int>();
            var results = new List<BulkNodeResultDto>();

            var allTempIds = request.Request.Nodes.Select(n => n.TempId).ToHashSet();
            foreach (var nodeDto in request.Request.Nodes)
            {
                if (!string.IsNullOrEmpty(nodeDto.ParentTempId) && !allTempIds.Contains(nodeDto.ParentTempId))
                {
                    _logger.LogDecision(_loggingOptions, LogAction.OrgNode.BulkSetupOrgNodes, LogStage.Processing,
                        "ParentTempIdNotFound", new { ParentTempId = nodeDto.ParentTempId });
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Failure<BulkSetupOrgNodesResponse>(DomainErrors.OrgNode.NotFound);
                }
            }

            var roots = request.Request.Nodes.Where(n => string.IsNullOrEmpty(n.ParentTempId)).ToList();
            foreach (var root in roots)
            {
                var node = new OrgNode
                {
                    Name = root.Name,
                    Type = root.Type?.Trim().ToLower(),
                    ParentId = null,
                    CompanyId = request.Request.CompanyId
                };

                await _unitOfWork.OrgNodes.AddAsync(node, cancellationToken);
                tempIdToNode[root.TempId] = node;
                tempIdToDepth[root.TempId] = 0;

                _logger.LogDecision(_loggingOptions, LogAction.OrgNode.BulkSetupOrgNodes, LogStage.Processing,
                    "RootNodeCreated", new { TempId = root.TempId, NodeId = node.Id, Name = node.Name });
            }

            var resolved = new HashSet<string>(tempIdToNode.Keys);
            var remaining = request.Request.Nodes.Where(n => !resolved.Contains(n.TempId)).ToList();

            while (remaining.Count > 0)
            {
                var madeProgress = false;

                foreach (var nodeDto in remaining.ToList())
                {
                    if (resolved.Contains(nodeDto.TempId))
                        continue;

                    if (!string.IsNullOrEmpty(nodeDto.ParentTempId) && !resolved.Contains(nodeDto.ParentTempId))
                        continue;

                    var parentId = string.IsNullOrEmpty(nodeDto.ParentTempId)
                        ? (Guid?)null
                        : tempIdToNode[nodeDto.ParentTempId].Id;

                    var parentDepth = string.IsNullOrEmpty(nodeDto.ParentTempId)
                        ? -1
                        : tempIdToDepth[nodeDto.ParentTempId];

                    var node = new OrgNode
                    {
                        Name = nodeDto.Name,
                        Type = nodeDto.Type?.Trim().ToLower(),
                        ParentId = parentId,
                        CompanyId = request.Request.CompanyId
                    };

                    await _unitOfWork.OrgNodes.AddAsync(node, cancellationToken);
                    tempIdToNode[nodeDto.TempId] = node;
                    tempIdToDepth[nodeDto.TempId] = parentDepth + 1;
                    resolved.Add(nodeDto.TempId);
                    remaining.Remove(nodeDto);
                    madeProgress = true;

                    _logger.LogDecision(_loggingOptions, LogAction.OrgNode.BulkSetupOrgNodes, LogStage.Processing,
                        "ChildNodeCreated", new { TempId = nodeDto.TempId, NodeId = node.Id, ParentTempId = nodeDto.ParentTempId ?? "root", Depth = parentDepth + 1 });
                }

                if (!madeProgress && remaining.Count > 0)
                {
                    _logger.LogDecision(_loggingOptions, LogAction.OrgNode.BulkSetupOrgNodes, LogStage.Processing,
                        "CircularReference", new { RemainingNodes = remaining.Select(n => n.TempId).ToList() });
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Failure<BulkSetupOrgNodesResponse>(DomainErrors.OrgNode.InvalidHierarchyConfiguration);
                }
            }

            foreach (var nodeDto in request.Request.Nodes)
            {
                var node = tempIdToNode[nodeDto.TempId];

                foreach (var assignmentDto in nodeDto.Assignments)
                {
                    var assignment = new OrgNodeAssignment
                    {
                        OrgNodeId = node.Id,
                        EmployeeId = assignmentDto.EmployeeId,
                        Role = assignmentDto.Role
                    };

                    await _unitOfWork.OrgNodeAssignments.AddAsync(assignment, cancellationToken);

                    _logger.LogDecision(_loggingOptions, LogAction.OrgNode.BulkSetupOrgNodes, LogStage.Processing,
                        "EmployeeAssigned", new { EmployeeId = assignmentDto.EmployeeId, NodeId = node.Id, Role = assignmentDto.Role });
                }

                results.Add(new BulkNodeResultDto
                {
                    TempId = nodeDto.TempId,
                    RealId = node.Id,
                    Name = node.Name,
                    Depth = tempIdToDepth[nodeDto.TempId]
                });
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            var companyNodeId = results.First(r => r.Depth == 0).RealId;

            return Result.Success(new BulkSetupOrgNodesResponse
            {
                CompanyNodeId = companyNodeId,
                Nodes = results
            });
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            _logger.LogActionFailure(_loggingOptions, LogAction.OrgNode.BulkSetupOrgNodes, LogStage.Processing, ex,
                new { CompanyId = request.Request.CompanyId, NodeCount = request.Request.Nodes.Count });
            throw;
        }
    }
}