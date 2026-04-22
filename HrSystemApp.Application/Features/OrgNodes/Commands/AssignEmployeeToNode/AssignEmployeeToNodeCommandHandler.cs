using System.Diagnostics;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Models;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using HrSystemApp.Application.Common.Logging;

namespace HrSystemApp.Application.Features.OrgNodes.Commands.AssignEmployeeToNode;

public class AssignEmployeeToNodeCommandHandler : IRequestHandler<AssignEmployeeToNodeCommand, Result<Guid>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<AssignEmployeeToNodeCommandHandler> _logger;
    private readonly LoggingOptions _loggingOptions;

    public AssignEmployeeToNodeCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ILogger<AssignEmployeeToNodeCommandHandler> logger,
        IOptions<LoggingOptions> loggingOptions)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _logger = logger;
        _loggingOptions = loggingOptions.Value;
    }

    public async Task<Result<Guid>> Handle(AssignEmployeeToNodeCommand request, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        _logger.LogActionStart(_loggingOptions, LogAction.OrgNode.AssignEmployeeToNode);

        var node = await _unitOfWork.OrgNodes.GetByIdAsync(request.OrgNodeId, cancellationToken);
        if (node == null)
        {
            _logger.LogDecision(_loggingOptions, LogAction.OrgNode.AssignEmployeeToNode, LogStage.Processing,
                "NodeNotFound", new { NodeId = request.OrgNodeId });
            sw.Stop();
            return Result.Failure<Guid>(DomainErrors.OrgNode.NotFound);
        }

        var employee = await _unitOfWork.Employees.GetByIdAsync(request.EmployeeId, cancellationToken);
        if (employee == null)
        {
            _logger.LogDecision(_loggingOptions, LogAction.OrgNode.AssignEmployeeToNode, LogStage.Processing,
                "EmployeeNotFound", new { EmployeeId = request.EmployeeId });
            sw.Stop();
            return Result.Failure<Guid>(DomainErrors.Employee.NotFound);
        }

        var exists = await _unitOfWork.OrgNodeAssignments.ExistsAsync(request.OrgNodeId, request.EmployeeId, cancellationToken);
        if (exists)
        {
            _logger.LogDecision(_loggingOptions, LogAction.OrgNode.AssignEmployeeToNode, LogStage.Processing,
                "DuplicateAssignment", new { EmployeeId = request.EmployeeId, NodeId = request.OrgNodeId });
            sw.Stop();
            return Result.Failure<Guid>(DomainErrors.OrgNode.DuplicateAssignment);
        }

        var assignment = new OrgNodeAssignment
        {
            OrgNodeId = request.OrgNodeId,
            EmployeeId = request.EmployeeId,
            Role = request.Role
        };

        await _unitOfWork.OrgNodeAssignments.AddAsync(assignment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        sw.Stop();
        _logger.LogActionSuccess(_loggingOptions, LogAction.OrgNode.AssignEmployeeToNode, sw.ElapsedMilliseconds);

        return Result.Success(assignment.Id);
    }
}