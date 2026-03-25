using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.ContactAdmin;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Domain.Models;
using Mapster;
using MediatR;


namespace HrSystemApp.Application.Features.ContactAdmin.Queries.GetContactAdminRequests;

public class GetContactAdminRequestsQueryHandler : IRequestHandler<GetContactAdminRequestsQuery,
    Result<PagedResult<ContactAdminRequestDto>>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetContactAdminRequestsQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<PagedResult<ContactAdminRequestDto>>> Handle(GetContactAdminRequestsQuery request,
        CancellationToken cancellationToken)
    {
        var pagedEntities = await _unitOfWork.ContactAdminRequests.GetPagedAsync(
            request.Status,
            request.PageNumber,
            request.PageSize,
            cancellationToken);

        var items = pagedEntities.Items.Adapt<List<ContactAdminRequestDto>>();

        return Result.Success(PagedResult<ContactAdminRequestDto>.Create(
            items, pagedEntities.PageNumber, pagedEntities.PageSize, pagedEntities.TotalCount));
    }
}
