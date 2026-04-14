using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.OrgNodes;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace HrSystemApp.Application.Features.OrgNodes.Queries.GetOrgNodeDetails;

public class GetOrgNodeDetailsQueryHandler : IRequestHandler<GetOrgNodeDetailsQuery, Result<OrgNodeDetailsResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<GetOrgNodeDetailsQueryHandler> _logger;

    public GetOrgNodeDetailsQueryHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ILogger<GetOrgNodeDetailsQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<Result<OrgNodeDetailsResponse>> Handle(GetOrgNodeDetailsQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting OrgNode details for {NodeId}", request.Id);

        var node = await _unitOfWork.OrgNodes.GetByIdWithChildrenAsync(request.Id, cancellationToken);
        if (node == null)
        {
            _logger.LogWarning("GetOrgNodeDetails failed: Node {NodeId} not found.", request.Id);
            return Result.Failure<OrgNodeDetailsResponse>(DomainErrors.OrgNode.NotFound);
        }

        // Get parent name
        string? parentName = null;
        if (node.ParentId.HasValue)
        {
            var parent = await _unitOfWork.OrgNodes.GetByIdAsync(node.ParentId.Value, cancellationToken);
            parentName = parent?.Name;
        }

        // Get linked entity name
        string? linkedEntityName = null;
        if (node.EntityId.HasValue && node.EntityType.HasValue)
        {
            linkedEntityName = node.EntityType switch
            {
                OrgEntityType.Department => (await _unitOfWork.Departments.GetByIdAsync(node.EntityId.Value, cancellationToken))?.Name,
                OrgEntityType.Unit => (await _unitOfWork.Units.GetByIdAsync(node.EntityId.Value, cancellationToken))?.Name,
                OrgEntityType.Team => (await _unitOfWork.Teams.GetByIdAsync(node.EntityId.Value, cancellationToken))?.Name,
                _ => null
            };
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
            c.LevelId,
            c.Level?.Name
        )).ToList();

        var response = new OrgNodeDetailsResponse(
            node.Id,
            node.Name,
            node.ParentId,
            parentName,
            node.LevelId,
            node.Level?.Name,
            node.EntityId,
            node.EntityType,
            linkedEntityName,
            assignmentResponses,
            childResponses
        );

        return Result.Success(response);
    }
}