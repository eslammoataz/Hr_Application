using HrSystemApp.Application.Common;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Models;
using MediatR;
using Microsoft.Extensions.Logging;

namespace HrSystemApp.Application.Features.HierarchyLevels.Commands.CreateHierarchyLevel;

public class CreateHierarchyLevelCommandHandler : IRequestHandler<CreateHierarchyLevelCommand, Result<Guid>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<CreateHierarchyLevelCommandHandler> _logger;

    public CreateHierarchyLevelCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ILogger<CreateHierarchyLevelCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<Result<Guid>> Handle(CreateHierarchyLevelCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Attempting to create HierarchyLevel: {Name}", request.Name);

        // Validate parent level exists if provided
        if (request.ParentLevelId.HasValue)
        {
            var parent = await _unitOfWork.HierarchyLevels.GetByIdAsync(request.ParentLevelId.Value, cancellationToken);
            if (parent == null)
            {
                _logger.LogWarning("CreateHierarchyLevel failed: Parent level {ParentLevelId} not found.", request.ParentLevelId);
                return Result.Failure<Guid>(DomainErrors.HierarchyLevel.NotFound);
            }
        }

        // Create the level
        var level = new HierarchyLevel
        {
            Name = request.Name,
            SortOrder = request.SortOrder,
            ParentLevelId = request.ParentLevelId
        };

        await _unitOfWork.HierarchyLevels.AddAsync(level, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Successfully created HierarchyLevel {LevelId}: {Name}", level.Id, level.Name);
        return Result.Success(level.Id);
    }
}