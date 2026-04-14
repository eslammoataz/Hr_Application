using HrSystemApp.Application.Common;
using MediatR;

namespace HrSystemApp.Application.Features.HierarchyLevels.Commands.DeleteHierarchyLevel;

public record DeleteHierarchyLevelCommand(Guid Id) : IRequest<Result<Guid>>;