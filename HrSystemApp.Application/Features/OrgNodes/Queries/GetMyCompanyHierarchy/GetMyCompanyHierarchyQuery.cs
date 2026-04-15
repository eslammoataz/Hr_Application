using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.OrgNodes;
using MediatR;

namespace HrSystemApp.Application.Features.OrgNodes.Queries.GetMyCompanyHierarchy;

public record GetMyCompanyHierarchyQuery(int? Depth = null) : IRequest<Result<List<OrgNodeTreeResponse>>>;