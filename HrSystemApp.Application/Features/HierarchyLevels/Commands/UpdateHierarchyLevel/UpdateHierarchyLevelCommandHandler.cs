using HrSystemApp.Application.Common;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace HrSystemApp.Application.Features.HierarchyLevels.Commands.UpdateHierarchyLevel;

public class UpdateHierarchyLevelCommandHandler : IRequestHandler<UpdateHierarchyLevelCommand, Result<Guid>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<UpdateHierarchyLevelCommandHandler> _logger;

    public UpdateHierarchyLevelCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ILogger<UpdateHierarchyLevelCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<Result<Guid>> Handle(UpdateHierarchyLevelCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Attempting to update HierarchyLevel {LevelId}", request.Id);

        var level = await _unitOfWork.HierarchyLevels.GetByIdAsync(request.Id, cancellationToken);
        if (level == null)
        {
            _logger.LogWarning("UpdateHierarchyLevel failed: Level {LevelId} not found.", request.Id);
            return Result.Failure<Guid>(DomainErrors.HierarchyLevel.NotFound);
        }

        // Validate parent level exists if provided
        if (request.ParentLevelId.HasValue)
        {
            if (request.ParentLevelId.Value == request.Id)
            {
                _logger.LogWarning("UpdateHierarchyLevel failed: Level cannot be its own parent.");
                return Result.Failure<Guid>(DomainErrors.HierarchyLevel.NotFound);
            }

            var parent = await _unitOfWork.HierarchyLevels.GetByIdAsync(request.ParentLevelId.Value, cancellationToken);
            if (parent == null)
            {
                _logger.LogWarning("UpdateHierarchyLevel failed: Parent level {ParentLevelId} not found.", request.ParentLevelId);
                return Result.Failure<Guid>(DomainErrors.HierarchyLevel.NotFound);
            }
        }

        level.Name = request.Name;
        level.SortOrder = request.SortOrder;
        level.ParentLevelId = request.ParentLevelId;

        await _unitOfWork.HierarchyLevels.UpdateAsync(level, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Successfully updated HierarchyLevel {LevelId}", level.Id);
        return Result.Success(level.Id);
    }
}