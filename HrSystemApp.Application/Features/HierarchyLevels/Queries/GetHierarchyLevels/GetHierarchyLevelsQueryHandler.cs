using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.HierarchyLevels;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace HrSystemApp.Application.Features.HierarchyLevels.Queries.GetHierarchyLevels;

public record GetHierarchyLevelsQuery : IRequest<Result<List<HierarchyLevelResponse>>>;

public class GetHierarchyLevelsQueryHandler : IRequestHandler<GetHierarchyLevelsQuery, Result<List<HierarchyLevelResponse>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<GetHierarchyLevelsQueryHandler> _logger;

    public GetHierarchyLevelsQueryHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ILogger<GetHierarchyLevelsQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<Result<List<HierarchyLevelResponse>>> Handle(GetHierarchyLevelsQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting all hierarchy levels");

        var levels = await _unitOfWork.HierarchyLevels.GetAllOrderedAsync(cancellationToken);

        var response = new List<HierarchyLevelResponse>();
        foreach (var level in levels)
        {
            var nodeCount = await _unitOfWork.OrgNodes.GetChildCountAsync(null, cancellationToken);
            // Note: GetChildCountAsync counts children by parent, so we need a different approach
            // For now, we'll get all nodes and filter by level
            var allNodes = await _unitOfWork.OrgNodes.GetAllAsync(cancellationToken);
            var count = allNodes.Count(n => n.LevelId == level.Id);

            string? parentName = null;
            if (level.ParentLevelId.HasValue)
            {
                var parent = await _unitOfWork.HierarchyLevels.GetByIdAsync(level.ParentLevelId.Value, cancellationToken);
                parentName = parent?.Name;
            }

            response.Add(new HierarchyLevelResponse(
                level.Id,
                level.Name,
                level.SortOrder,
                level.ParentLevelId,
                parentName,
                count
            ));
        }

        return Result.Success(response);
    }
}