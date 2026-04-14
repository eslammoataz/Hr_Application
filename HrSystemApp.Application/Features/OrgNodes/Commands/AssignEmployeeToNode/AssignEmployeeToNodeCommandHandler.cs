using HrSystemApp.Application.Common;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Models;
using MediatR;
using Microsoft.Extensions.Logging;

namespace HrSystemApp.Application.Features.OrgNodes.Commands.AssignEmployeeToNode;

public class AssignEmployeeToNodeCommandHandler : IRequestHandler<AssignEmployeeToNodeCommand, Result<Guid>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<AssignEmployeeToNodeCommandHandler> _logger;

    public AssignEmployeeToNodeCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ILogger<AssignEmployeeToNodeCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<Result<Guid>> Handle(AssignEmployeeToNodeCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Assigning Employee {EmployeeId} to OrgNode {OrgNodeId} with role {Role}",
            request.EmployeeId, request.OrgNodeId, request.Role);

        // Validate node exists
        var node = await _unitOfWork.OrgNodes.GetByIdAsync(request.OrgNodeId, cancellationToken);
        if (node == null)
        {
            _logger.LogWarning("AssignEmployeeToNode failed: Node {OrgNodeId} not found.", request.OrgNodeId);
            return Result.Failure<Guid>(DomainErrors.OrgNode.NotFound);
        }

        // Validate employee exists
        var employee = await _unitOfWork.Employees.GetByIdAsync(request.EmployeeId, cancellationToken);
        if (employee == null)
        {
            _logger.LogWarning("AssignEmployeeToNode failed: Employee {EmployeeId} not found.", request.EmployeeId);
            return Result.Failure<Guid>(DomainErrors.Employee.NotFound);
        }

        // Check for duplicate assignment
        var exists = await _unitOfWork.OrgNodeAssignments.ExistsAsync(request.OrgNodeId, request.EmployeeId, cancellationToken);
        if (exists)
        {
            _logger.LogWarning("AssignEmployeeToNode failed: Employee {EmployeeId} is already assigned to node {OrgNodeId}.",
                request.EmployeeId, request.OrgNodeId);
            return Result.Failure<Guid>(DomainErrors.OrgNode.DuplicateAssignment);
        }

        // Create the assignment
        var assignment = new OrgNodeAssignment
        {
            OrgNodeId = request.OrgNodeId,
            EmployeeId = request.EmployeeId,
            Role = request.Role
        };

        await _unitOfWork.OrgNodeAssignments.AddAsync(assignment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Successfully assigned Employee {EmployeeId} to OrgNode {OrgNodeId}",
            request.EmployeeId, request.OrgNodeId);
        return Result.Success(assignment.Id);
    }
}