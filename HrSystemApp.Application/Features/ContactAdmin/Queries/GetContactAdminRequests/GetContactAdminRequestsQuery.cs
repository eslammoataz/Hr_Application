using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.ContactAdmin;
using HrSystemApp.Domain.Enums;
using MediatR;

namespace HrSystemApp.Application.Features.ContactAdmin.Queries.GetContactAdminRequests;

public record GetContactAdminRequestsQuery(
    ContactAdminRequestStatus? Status = null,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<Result<PagedResult<ContactAdminRequestDto>>>;
