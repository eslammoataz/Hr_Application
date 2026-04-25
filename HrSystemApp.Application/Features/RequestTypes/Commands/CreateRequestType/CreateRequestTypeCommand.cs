using HrSystemApp.Application.Common;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Models;
using MediatR;

namespace HrSystemApp.Application.Features.RequestTypes.Commands;

public record CreateRequestTypeCommand : IRequest<Result<Guid>>
{
    public string KeyName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? FormSchemaJson { get; set; }
    public bool AllowExtraFields { get; set; } = true;
    public string? RequestNumberPattern { get; set; }
    public int? DefaultSlaDays { get; set; }
    public string? DisplayNameLocalizationsJson { get; set; }
}

public class CreateRequestTypeCommandHandler : IRequestHandler<CreateRequestTypeCommand, Result<Guid>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public CreateRequestTypeCommandHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result<Guid>> Handle(CreateRequestTypeCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return Result.Failure<Guid>(DomainErrors.Auth.Unauthorized);

        var employee = await _unitOfWork.Employees.GetByUserIdAsync(userId, cancellationToken);
        if (employee == null)
            return Result.Failure<Guid>(DomainErrors.Employee.NotFound);

        // Check if a custom type with this key name already exists for this company
        var existing = await _unitOfWork.RequestTypes.GetByKeyNameAsync(request.KeyName, employee.CompanyId, cancellationToken);
        if (existing != null)
            return Result.Failure<Guid>(DomainErrors.Requests.DefinitionAlreadyExists);

        // Validate key name format (alphanumeric, lowercase, no spaces)
        if (!System.Text.RegularExpressions.Regex.IsMatch(request.KeyName, @"^[a-z][a-z0-9]*$"))
            return Result.Failure<Guid>(DomainErrors.Validation.Error with { Message = "KeyName must be lowercase alphanumeric, starting with a letter." });

        var requestType = new RequestType
        {
            KeyName = request.KeyName,
            DisplayName = request.DisplayName,
            IsSystemType = false,
            IsCustomType = true,
            CompanyId = employee.CompanyId,
            FormSchemaJson = request.FormSchemaJson,
            AllowExtraFields = request.AllowExtraFields,
            RequestNumberPattern = request.RequestNumberPattern,
            DefaultSlaDays = request.DefaultSlaDays,
            DisplayNameLocalizationsJson = request.DisplayNameLocalizationsJson,
            Version = 1
        };

        await _unitOfWork.RequestTypes.AddAsync(requestType, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(requestType.Id);
    }
}
