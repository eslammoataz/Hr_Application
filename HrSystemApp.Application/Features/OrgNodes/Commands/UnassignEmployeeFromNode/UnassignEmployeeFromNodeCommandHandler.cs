using HrSystemApp.Application.Common;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace HrSystemApp.Application.Features.OrgNodes.Commands.UnassignEmployeeFromNode;

public class UnassignEmployeeFromNodeCommandHandler : IRequestHandler<UnassignEmployeeFromNodeCommand, Result<Guid>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<UnassignEmployeeFromNodeCommandHandler> _logger;

    public UnassignEmployeeFromNodeCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ILogger<UnassignEmployeeFromNodeCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<Result<Guid>> Handle(UnassignEmployeeFromNodeCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Unassigning Employee {EmployeeId} from OrgNode {OrgNodeId}",
            request.EmployeeId, request.OrgNodeId);

        var assignment = await _unitOfWork.OrgNodeAssignments.GetByNodeAndEmployeeAsync(
            request.OrgNodeId, request.EmployeeId, cancellationToken);

        if (assignment == null)
        {
            _logger.LogWarning("UnassignEmployeeFromNode failed: Assignment not found.");
            return Result.Failure<Guid>(DomainErrors.OrgNode.AssignmentNotFound);
        }

        await _unitOfWork.OrgNodeAssignments.DeleteAsync(assignment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Successfully unassigned Employee {EmployeeId} from OrgNode {OrgNodeId}",
            request.EmployeeId, request.OrgNodeId);
        return Result.Success(assignment.Id);
    }
}