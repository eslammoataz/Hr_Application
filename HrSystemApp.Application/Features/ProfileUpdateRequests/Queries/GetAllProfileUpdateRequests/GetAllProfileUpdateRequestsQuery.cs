using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Employees.ProfileUpdateRequests;
using HrSystemApp.Domain.Enums;
using MediatR;

namespace HrSystemApp.Application.Features.ProfileUpdateRequests.Queries.GetAllProfileUpdateRequests;

public record GetAllProfileUpdateRequestsQuery(string HrUserId, ProfileUpdateRequestStatus? Status, int PageNumber = 1, int PageSize = 20)
    : IRequest<Result<PagedResult<ProfileUpdateRequestDto>>>;
