using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.ContactAdmin;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Repositories;
using HrSystemApp.Domain.Models;
using Mapster;
using MediatR;
using HrSystemApp.Domain.Enums;

namespace HrSystemApp.Application.Features.ContactAdmin.Queries.GetContactAdminRequests;

public class GetContactAdminRequestsQueryHandler : IRequestHandler<GetContactAdminRequestsQuery,
    Result<ContactAdminPagedResult>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetContactAdminRequestsQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ContactAdminPagedResult>> Handle(GetContactAdminRequestsQuery request,
        CancellationToken cancellationToken)
    {
        var pagedEntities = await _unitOfWork.ContactAdminRequests.GetPagedAsync(
            request.IsAccepted,
            request.IsPending,
            request.IsRejected,
            request.PageNumber,
            request.PageSize,
            cancellationToken);

        // Map items
        var items = pagedEntities.Items.Adapt<List<ContactAdminRequestDto>>();

        // Fetch all status counts in a single query
        var (pending, accepted, rejected) =
            await _unitOfWork.ContactAdminRequests.GetStatusCountsAsync(cancellationToken);

        // Use Mapster to map base properties and set the specialized ones
        var result = new ContactAdminPagedResult
        {
            Items = items,
            PageNumber = pagedEntities.PageNumber,
            PageSize = pagedEntities.PageSize,
            TotalCount = pagedEntities.TotalCount,
            TotalPending = pending,
            TotalAccepted = accepted,
            TotalRejected = rejected
        };

        return Result.Success(result);
    }
}
