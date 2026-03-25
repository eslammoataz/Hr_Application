using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.ContactAdmin;
using MediatR;

namespace HrSystemApp.Application.Features.ContactAdmin.Queries.GetContactAdminRequests;

public record GetContactAdminRequestsQuery(
    bool? IsAccepted = null,
    bool? IsPending = null,
    bool? IsRejected = null,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<Result<PagedResult<ContactAdminRequestDto>>>;
