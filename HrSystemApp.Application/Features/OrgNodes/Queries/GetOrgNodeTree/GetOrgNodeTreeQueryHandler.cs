using HrSystemApp.Application.Common;
using HrSystemApp.Application.Common.Logging;
using HrSystemApp.Application.DTOs.OrgNodes;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Models;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HrSystemApp.Application.Features.OrgNodes.Queries.GetOrgNodeTree;

public class GetOrgNodeTreeQueryHandler : IRequestHandler<GetOrgNodeTreeQuery, Result<List<OrgNodeTreeResponse>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<GetOrgNodeTreeQueryHandler> _logger;
    private readonly LoggingOptions _loggingOptions;

    public GetOrgNodeTreeQueryHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ILogger<GetOrgNodeTreeQueryHandler> logger,
        IOptions<LoggingOptions> loggingOptions)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _logger = logger;
        _loggingOptions = loggingOptions.Value;
    }

    public async Task<Result<List<OrgNodeTreeResponse>>> Handle(GetOrgNodeTreeQuery request, CancellationToken cancellationToken)
    {

        var depth = request.Depth ?? 1;

        var startNodes = request.ParentId.HasValue
            ? await _unitOfWork.OrgNodes.GetChildrenAsync(request.ParentId, cancellationToken)
            : await _unitOfWork.OrgNodes.GetRootNodesAsync(cancellationToken);

        if (depth <= 0)
        {
            return Result.Success(new List<OrgNodeTreeResponse>());
        }

        if (!string.IsNullOrWhiteSpace(request.Type))
        {
            var normalizedType = request.Type.Trim().ToLower();
            startNodes = startNodes.Where(n => n.Type == normalizedType).ToList();
        }

        var result = new List<OrgNodeTreeResponse>();
        await BuildTreeAsync(startNodes, depth - 1, result, cancellationToken);

        return Result.Success(result);
    }

    private async Task BuildTreeAsync(
        IReadOnlyList<OrgNode> nodes,
        int remainingDepth,
        List<OrgNodeTreeResponse> accumulator,
        CancellationToken ct)
    {
        foreach (var node in nodes)
        {
            // Get child count for HasChildren flag
            var childCount = await _unitOfWork.OrgNodes.GetChildCountAsync(node.Id, ct);

            // Map assignments from already-loaded node.Assignments
            var assignmentResponses = node.Assignments?.Select(a => new OrgNodeAssignmentResponse(
                a.Id,
                a.EmployeeId,
                a.Employee?.FullName ?? string.Empty,
                a.Employee?.Email,
                a.Role,
                a.Role.ToString()
            )).ToList() ?? new List<OrgNodeAssignmentResponse>();

            var response = new OrgNodeTreeResponse(
                node.Id,
                node.Name,
                childCount > 0,
                new List<OrgNodeTreeResponse>(),
                node.Type,
                assignmentResponses);

            // If remainingDepth > 0, recursively load children
            if (remainingDepth > 0 && childCount > 0)
            {
                var children = await _unitOfWork.OrgNodes.GetChildrenAsync(node.Id, ct);
                await BuildTreeAsync(children, remainingDepth - 1, response.Children, ct);
            }

            accumulator.Add(response);
        }
    }
}
