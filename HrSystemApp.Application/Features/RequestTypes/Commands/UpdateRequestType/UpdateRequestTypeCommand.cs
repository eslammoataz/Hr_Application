using HrSystemApp.Application.Common;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Models;
using MediatR;

namespace HrSystemApp.Application.Features.RequestTypes.Commands;

public record UpdateRequestTypeCommand : IRequest<Result<Guid>>
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? FormSchemaJson { get; set; }
    public bool AllowExtraFields { get; set; } = true;
    public string? RequestNumberPattern { get; set; }
    public int? DefaultSlaDays { get; set; }
    public string? DisplayNameLocalizationsJson { get; set; }
}

public class UpdateRequestTypeCommandHandler : IRequestHandler<UpdateRequestTypeCommand, Result<Guid>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public UpdateRequestTypeCommandHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result<Guid>> Handle(UpdateRequestTypeCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return Result.Failure<Guid>(DomainErrors.Auth.Unauthorized);

        var employee = await _unitOfWork.Employees.GetByUserIdAsync(userId, cancellationToken);
        if (employee == null)
            return Result.Failure<Guid>(DomainErrors.Employee.NotFound);

        var requestType = await _unitOfWork.RequestTypes.GetByIdAsync(request.Id, cancellationToken);
        if (requestType == null)
            return Result.Failure<Guid>(DomainErrors.Requests.NotFound);

        // Cannot modify system types
        if (requestType.IsSystemType)
            return Result.Failure<Guid>(DomainErrors.Requests.Locked);

        // Cannot modify types from other companies
        if (requestType.CompanyId != employee.CompanyId)
            return Result.Failure<Guid>(DomainErrors.Auth.Unauthorized);

        // Update fields
        requestType.DisplayName = request.DisplayName;
        requestType.FormSchemaJson = request.FormSchemaJson;
        requestType.AllowExtraFields = request.AllowExtraFields;
        requestType.RequestNumberPattern = request.RequestNumberPattern;
        requestType.DefaultSlaDays = request.DefaultSlaDays;
        requestType.DisplayNameLocalizationsJson = request.DisplayNameLocalizationsJson;
        requestType.Version++;

        await _unitOfWork.RequestTypes.UpdateAsync(requestType, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(requestType.Id);
    }
}
