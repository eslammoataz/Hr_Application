using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.OrgNodes;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Models;
using MediatR;
using Microsoft.Extensions.Logging;

namespace HrSystemApp.Application.Features.OrgNodes.Queries.GetMyCompanyHierarchy;

public class GetMyCompanyHierarchyQueryHandler : IRequestHandler<GetMyCompanyHierarchyQuery, Result<List<OrgNodeTreeResponse>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<GetMyCompanyHierarchyQueryHandler> _logger;

    public GetMyCompanyHierarchyQueryHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ILogger<GetMyCompanyHierarchyQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<Result<List<OrgNodeTreeResponse>>> Handle(GetMyCompanyHierarchyQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("GetMyCompanyHierarchy failed: User not authenticated.");
            return Result.Failure<List<OrgNodeTreeResponse>>(DomainErrors.Auth.Unauthorized);
        }

        var employee = await _unitOfWork.Employees.GetByUserIdAsync(userId, cancellationToken);
        if (employee == null)
        {
            _logger.LogWarning("GetMyCompanyHierarchy failed: Employee not found for user {UserId}.", userId);
            return Result.Failure<List<OrgNodeTreeResponse>>(DomainErrors.Employee.NotFound);
        }

        var assignment = await _unitOfWork.OrgNodeAssignments.GetByEmployeeWithNodeAsync(employee.Id, cancellationToken);
        if (assignment == null)
        {
            _logger.LogInformation("GetMyCompanyHierarchy: Employee {EmployeeId} is not assigned to any node.", employee.Id);
            return Result.Success(new List<OrgNodeTreeResponse>());
        }

        var myNode = assignment.OrgNode;

        // Walk up to find the root node
        var rootNode = await GetRootNodeAsync(myNode.Id, cancellationToken);

        var depth = request.Depth ?? 10; // default to full tree
        _logger.LogInformation("Getting company hierarchy for root node {RootNodeId}, depth {Depth}", rootNode.Id, depth);

        var result = new List<OrgNodeTreeResponse>();
        await BuildTreeAsync(rootNode, depth - 1, result, cancellationToken);

        return Result.Success(result);
    }

    private async Task<OrgNode> GetRootNodeAsync(Guid nodeId, CancellationToken ct)
    {
        var node = await _unitOfWork.OrgNodes.GetByIdAsync(nodeId, ct);
        if (node == null)
            throw new InvalidOperationException($"Node {nodeId} not found.");

        while (node.ParentId.HasValue)
        {
            node = await _unitOfWork.OrgNodes.GetByIdAsync(node.ParentId.Value, ct);
            if (node == null)
                throw new InvalidOperationException($"Parent node {node.ParentId} not found.");
        }

        return node;
    }

    private async Task BuildTreeAsync(
        OrgNode node,
        int remainingDepth,
        List<OrgNodeTreeResponse> accumulator,
        CancellationToken ct)
    {
        var childCount = await _unitOfWork.OrgNodes.GetChildCountAsync(node.Id, ct);

        var assignments = await _unitOfWork.OrgNodeAssignments.GetByNodeAsync(node.Id, ct);
        var assignmentResponses = assignments.Select(a => new OrgNodeAssignmentResponse(
            a.Id,
            a.EmployeeId,
            a.Employee?.FullName ?? string.Empty,
            a.Employee?.Email,
            a.Role,
            a.Role.ToString()
        )).ToList();

        var response = new OrgNodeTreeResponse(
            node.Id,
            node.Name,
            childCount > 0,
            new List<OrgNodeTreeResponse>(),
            node.Type,
            assignmentResponses);

        if (remainingDepth > 0 && childCount > 0)
        {
            var children = await _unitOfWork.OrgNodes.GetChildrenAsync(node.Id, ct);
            foreach (var child in children)
            {
                await BuildTreeAsync(child, remainingDepth - 1, response.Children, ct);
            }
        }

        accumulator.Add(response);
    }
}
