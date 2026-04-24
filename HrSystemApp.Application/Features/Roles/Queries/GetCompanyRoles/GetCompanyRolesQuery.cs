using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Roles;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Models;
using MediatR;

namespace HrSystemApp.Application.Features.Roles.Queries.GetCompanyRoles;

public sealed record GetCompanyRolesQuery : IRequest<Result<PagedResult<CompanyRoleDto>>>
{
    private const int MaxPageSize = 100;
    private int _pageSize = 10;
    private int _pageNumber = 1;

    public GetCompanyRolesQuery(int pageNumber = 1, int pageSize = 10)
    {
        PageNumber = pageNumber;
        PageSize = pageSize;
    }

    public int PageNumber
    {
        get => _pageNumber;
        set => _pageNumber = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value > MaxPageSize ? MaxPageSize : value < 1 ? 1 : value;
    }
}

public class GetCompanyRolesQueryHandler : IRequestHandler<GetCompanyRolesQuery, Result<PagedResult<CompanyRoleDto>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public GetCompanyRolesQueryHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result<PagedResult<CompanyRoleDto>>> Handle(
        GetCompanyRolesQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(userId))
            return Result.Failure<PagedResult<CompanyRoleDto>>(DomainErrors.Auth.Unauthorized);

        var employee = await _unitOfWork.Employees.GetByUserIdAsync(userId, cancellationToken);
        if (employee is null)
            return Result.Failure<PagedResult<CompanyRoleDto>>(DomainErrors.Employee.NotFound);

        var queryable = _unitOfWork.CompanyRoles.QueryByCompanyId(employee.CompanyId);

        var totalCount = await _unitOfWork.CompanyRoles.CountAsync(queryable, cancellationToken);

        var roles = await _unitOfWork.CompanyRoles.ToListAsync(
            queryable.OrderBy(r => r.Name)
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize),
            cancellationToken);

        var dtos = roles.Select(r => new CompanyRoleDto(
            r.Id,
            r.Name,
            r.Description,
            r.Permissions.Select(p => p.Permission).ToList()
        )).ToList();

        return Result.Success(PagedResult<CompanyRoleDto>.Create(dtos, request.PageNumber, request.PageSize, totalCount));
    }
}
