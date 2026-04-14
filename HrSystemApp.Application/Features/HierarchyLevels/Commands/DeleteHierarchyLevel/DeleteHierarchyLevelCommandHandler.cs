using HrSystemApp.Application.Common;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace HrSystemApp.Application.Features.HierarchyLevels.Commands.DeleteHierarchyLevel;

public class DeleteHierarchyLevelCommandHandler : IRequestHandler<DeleteHierarchyLevelCommand, Result<Guid>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<DeleteHierarchyLevelCommandHandler> _logger;

    public DeleteHierarchyLevelCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ILogger<DeleteHierarchyLevelCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<Result<Guid>> Handle(DeleteHierarchyLevelCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Attempting to delete HierarchyLevel {LevelId}", request.Id);

        var level = await _unitOfWork.HierarchyLevels.GetByIdAsync(request.Id, cancellationToken);
        if (level == null)
        {
            _logger.LogWarning("DeleteHierarchyLevel failed: Level {LevelId} not found.", request.Id);
            return Result.Failure<Guid>(DomainErrors.HierarchyLevel.NotFound);
        }

        // Check if any nodes are assigned to this level
        var hasNodes = await _unitOfWork.HierarchyLevels.HasNodesAsync(request.Id, cancellationToken);
        if (hasNodes)
        {
            _logger.LogWarning("DeleteHierarchyLevel failed: Level {LevelId} has assigned nodes.", request.Id);
            return Result.Failure<Guid>(DomainErrors.HierarchyLevel.HasAssignedNodes);
        }

        await _unitOfWork.HierarchyLevels.DeleteAsync(level, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Successfully deleted HierarchyLevel {LevelId}", request.Id);
        return Result.Success(request.Id);
    }
}