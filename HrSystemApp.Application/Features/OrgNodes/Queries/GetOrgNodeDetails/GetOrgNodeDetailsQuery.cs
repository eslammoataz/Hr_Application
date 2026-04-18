using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.OrgNodes;
using MediatR;

namespace HrSystemApp.Application.Features.OrgNodes.Queries.GetOrgNodeDetails;

public record GetOrgNodeDetailsQuery(Guid Id) : IRequest<Result<OrgNodeDetailsResponse>>;