using HrSystemApp.Application.Common;
using HrSystemApp.Application.Common.Logging;
using HrSystemApp.Application.DTOs.OrgNodes;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HrSystemApp.Application.Features.OrgNodes.Queries.GetOrgNodeDetails;

public class GetOrgNodeDetailsQueryHandler : IRequestHandler<GetOrgNodeDetailsQuery, Result<OrgNodeDetailsResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<GetOrgNodeDetailsQueryHandler> _logger;
    private readonly LoggingOptions _loggingOptions;

    public GetOrgNodeDetailsQueryHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ILogger<GetOrgNodeDetailsQueryHandler> logger,
        IOptions<LoggingOptions> loggingOptions)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _logger = logger;
        _loggingOptions = loggingOptions.Value;
    }

    public async Task<Result<OrgNodeDetailsResponse>> Handle(GetOrgNodeDetailsQuery request, CancellationToken cancellationToken)
    {

        var node = await _unitOfWork.OrgNodes.GetByIdWithChildrenAsync(request.Id, cancellationToken);
        if (node == null)
        {
            _logger.LogDecision(_loggingOptions, LogAction.OrgNode.GetOrgNodeDetails, LogStage.Validation,
                "NodeNotFound", new { NodeId = request.Id });
            return Result.Failure<OrgNodeDetailsResponse>(DomainErrors.OrgNode.NotFound);
        }

        // Get parent name
        string? parentName = null;
        if (node.ParentId.HasValue)
        {
            var parent = await _unitOfWork.OrgNodes.GetByIdAsync(node.ParentId.Value, cancellationToken);
            parentName = parent?.Name;
        }

        // Get assignments
        var assignments = await _unitOfWork.OrgNodeAssignments.GetByNodeAsync(node.Id, cancellationToken);
        var assignmentResponses = assignments.Select(a => new OrgNodeAssignmentResponse(
            a.Id,
            a.EmployeeId,
            a.Employee.FullName,
            a.Employee.Email,
            a.Role,
            a.Role.ToString()
        )).ToList();

        // Get children
        var children = await _unitOfWork.OrgNodes.GetChildrenAsync(node.Id, cancellationToken);
        var childResponses = children.Select(c => new OrgNodeChildResponse(
            c.Id,
            c.Name,
            c.Type
        )).ToList();

        var response = new OrgNodeDetailsResponse(
            node.Id,
            node.Name,
            node.ParentId,
            parentName,
            node.Type,
            assignmentResponses,
            childResponses);

        return Result.Success(response);
    }
}
