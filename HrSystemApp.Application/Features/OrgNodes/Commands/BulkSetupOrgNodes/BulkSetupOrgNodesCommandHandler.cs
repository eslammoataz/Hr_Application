using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.OrgNodes;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Domain.Models;
using MediatR;
using Microsoft.Extensions.Logging;

namespace HrSystemApp.Application.Features.OrgNodes.Commands.BulkSetupOrgNodes;

public class BulkSetupOrgNodesCommandHandler : IRequestHandler<BulkSetupOrgNodesCommand, Result<BulkSetupOrgNodesResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<BulkSetupOrgNodesCommandHandler> _logger;

    public BulkSetupOrgNodesCommandHandler(IUnitOfWork unitOfWork, ILogger<BulkSetupOrgNodesCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<BulkSetupOrgNodesResponse>> Handle(BulkSetupOrgNodesCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting bulk org nodes setup with {Count} nodes", request.Request.Nodes.Count);

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var tempIdToNode = new Dictionary<string, OrgNode>();
            var tempIdToDepth = new Dictionary<string, int>();
            var results = new List<BulkNodeResultDto>();

            // Build dependency graph and validate all parentTempIds exist
            var allTempIds = request.Request.Nodes.Select(n => n.TempId).ToHashSet();
            foreach (var nodeDto in request.Request.Nodes)
            {
                if (!string.IsNullOrEmpty(nodeDto.ParentTempId) && !allTempIds.Contains(nodeDto.ParentTempId))
                {
                    _logger.LogWarning("BulkSetup failed: ParentTempId '{ParentTempId}' not found in request", nodeDto.ParentTempId);
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Failure<BulkSetupOrgNodesResponse>(DomainErrors.OrgNode.NotFound);
                }
            }

            // First pass: create all root nodes (no parent)
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

                _logger.LogInformation("Created root node: {TempId} -> {NodeId} ({Name})", root.TempId, node.Id, node.Name);
            }

            // Second pass: resolve dependencies in correct order using topological sort
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
                        continue; // parent not resolved yet

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

                    _logger.LogInformation("Created node: {TempId} -> {NodeId} (Parent: {ParentTempId}, Depth: {Depth})",
                        nodeDto.TempId, node.Id, nodeDto.ParentTempId ?? "null", parentDepth + 1);
                }

                // Safety check for circular references
                if (!madeProgress && remaining.Count > 0)
                {
                    _logger.LogError("BulkSetup failed: circular reference detected among remaining nodes: {Remaining}",
                        string.Join(", ", remaining.Select(n => n.TempId)));
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Failure<BulkSetupOrgNodesResponse>(DomainErrors.OrgNode.InvalidHierarchyConfiguration);
                }
            }

            // Third pass: create assignments
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
                    _logger.LogInformation("Assigned Employee {EmployeeId} to node {NodeId} with role {Role}",
                        assignmentDto.EmployeeId, node.Id, assignmentDto.Role);
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

            // Company node is the root with depth 0
            var companyNodeId = results.First(r => r.Depth == 0).RealId;

            _logger.LogInformation("BulkSetup completed successfully. Created {Count} nodes and assignments.", results.Count);

            return Result.Success(new BulkSetupOrgNodesResponse
            {
                CompanyNodeId = companyNodeId,
                Nodes = results
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during BulkSetupOrgNodes.");
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}
