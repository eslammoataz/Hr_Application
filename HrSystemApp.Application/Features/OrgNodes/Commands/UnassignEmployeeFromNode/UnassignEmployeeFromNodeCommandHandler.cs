using HrSystemApp.Application.Common;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using HrSystemApp.Application.Common.Logging;

namespace HrSystemApp.Application.Features.OrgNodes.Commands.UnassignEmployeeFromNode;

public class UnassignEmployeeFromNodeCommandHandler : IRequestHandler<UnassignEmployeeFromNodeCommand, Result<Guid>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<UnassignEmployeeFromNodeCommandHandler> _logger;
    private readonly LoggingOptions _loggingOptions;

    public UnassignEmployeeFromNodeCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ILogger<UnassignEmployeeFromNodeCommandHandler> logger,
        IOptions<LoggingOptions> loggingOptions)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _logger = logger;
        _loggingOptions = loggingOptions.Value;
    }

    public async Task<Result<Guid>> Handle(UnassignEmployeeFromNodeCommand request, CancellationToken cancellationToken)
    {

        var assignment = await _unitOfWork.OrgNodeAssignments.GetByNodeAndEmployeeAsync(
            request.OrgNodeId, request.EmployeeId, cancellationToken);

        if (assignment == null)
        {
            _logger.LogDecision(_loggingOptions, LogAction.OrgNode.UnassignEmployeeFromNode, LogStage.Processing,
                "AssignmentNotFound", new { NodeId = request.OrgNodeId, EmployeeId = request.EmployeeId });
            return Result.Failure<Guid>(DomainErrors.OrgNode.AssignmentNotFound);
        }

        await _unitOfWork.OrgNodeAssignments.DeleteAsync(assignment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(assignment.Id);
    }
}