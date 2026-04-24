using HrSystemApp.Application.Common;
using HrSystemApp.Application.Common.Logging;
using HrSystemApp.Application.DTOs.OrgNodes;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Models;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HrSystemApp.Application.Features.OrgNodes.Queries.GetMyCompanyHierarchy;

public class GetMyCompanyHierarchyQueryHandler : IRequestHandler<GetMyCompanyHierarchyQuery, Result<List<OrgNodeTreeResponse>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<GetMyCompanyHierarchyQueryHandler> _logger;
    private readonly LoggingOptions _loggingOptions;

    public GetMyCompanyHierarchyQueryHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ILogger<GetMyCompanyHierarchyQueryHandler> logger,
        IOptions<LoggingOptions> loggingOptions)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _logger = logger;
        _loggingOptions = loggingOptions.Value;
    }

    public async Task<Result<List<OrgNodeTreeResponse>>> Handle(GetMyCompanyHierarchyQuery request, CancellationToken cancellationToken)
    {

        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogDecision(_loggingOptions, LogAction.OrgNode.GetMyCompanyHierarchy, LogStage.Authorization,
                "UserNotAuthenticated", null);
            return Result.Failure<List<OrgNodeTreeResponse>>(DomainErrors.Auth.Unauthorized);
        }

        var employee = await _unitOfWork.Employees.GetByUserIdAsync(userId, cancellationToken);
        if (employee == null)
        {
            _logger.LogDecision(_loggingOptions, LogAction.OrgNode.GetMyCompanyHierarchy, LogStage.Authorization,
                "EmployeeNotFound", new { UserId = userId });
            return Result.Failure<List<OrgNodeTreeResponse>>(DomainErrors.Employee.NotFound);
        }

        var assignment = await _unitOfWork.OrgNodeAssignments.GetByEmployeeWithNodeAsync(employee.Id, cancellationToken);
        if (assignment == null)
        {
            _logger.LogDecision(_loggingOptions, LogAction.OrgNode.GetMyCompanyHierarchy, LogStage.Authorization,
                "EmployeeNotAssignedToNode", new { EmployeeId = employee.Id });
            return Result.Success(new List<OrgNodeTreeResponse>());
        }

        var myNode = assignment.OrgNode;

        var rootNodeResult = await GetRootNodeAsync(myNode.Id, cancellationToken);
        if (rootNodeResult.IsFailure)
            return Result.Failure<List<OrgNodeTreeResponse>>(rootNodeResult.Error);

        var rootNode = rootNodeResult.Value;

        var depth = request.Depth ?? 10;
        _logger.LogDecision(_loggingOptions, LogAction.OrgNode.GetMyCompanyHierarchy, LogStage.Processing,
            "BuildingHierarchy", new { RootNodeId = rootNode.Id, Depth = depth });

        var result = new List<OrgNodeTreeResponse>();
        await BuildTreeAsync(new[] { rootNode }, depth - 1, result, cancellationToken);

        return Result.Success(result);
    }

    private async Task<Result<OrgNode>> GetRootNodeAsync(Guid nodeId, CancellationToken ct)
    {
        var node = await _unitOfWork.OrgNodes.GetByIdAsync(nodeId, ct);
        if (node == null)
            return Result.Failure<OrgNode>(DomainErrors.OrgNode.NotFound);

        while (node.ParentId.HasValue)
        {
            var parentId = node.ParentId.Value;
            node = await _unitOfWork.OrgNodes.GetByIdAsync(parentId, ct);
            if (node == null)
                return Result.Failure<OrgNode>(DomainErrors.OrgNode.NotFound);
        }

        return Result.Success(node);
    }

    private async Task BuildTreeAsync(
        IReadOnlyList<OrgNode> nodes,
        int remainingDepth,
        List<OrgNodeTreeResponse> accumulator,
        CancellationToken ct)
    {
        if (nodes.Count == 0) return;

        // Batch-fetch child counts for all nodes at this level in a single query
        var nodeIds = nodes.Select(n => n.Id).ToList();
        var childCounts = await _unitOfWork.OrgNodes.GetChildCountsAsync(nodeIds, ct);

        foreach (var node in nodes)
        {
            var childCount = childCounts.TryGetValue(node.Id, out var count) ? count : 0;

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
                await BuildTreeAsync(children, remainingDepth - 1, response.Children, ct);
            }

            accumulator.Add(response);
        }
    }
}
