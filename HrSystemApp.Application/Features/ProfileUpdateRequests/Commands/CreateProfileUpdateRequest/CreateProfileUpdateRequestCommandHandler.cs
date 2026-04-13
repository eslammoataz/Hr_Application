using HrSystemApp.Application.Common;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using MediatR;
using System.Text.Json;

namespace HrSystemApp.Application.Features.ProfileUpdateRequests.Commands.CreateProfileUpdateRequest;

public class CreateProfileUpdateRequestCommandHandler : IRequestHandler<CreateProfileUpdateRequestCommand, Result>
{
    private readonly IUnitOfWork _unitOfWork;

    public CreateProfileUpdateRequestCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    private static readonly string[] AllowedFields =
    {
        nameof(Employee.FullName),
        nameof(Employee.PhoneNumber),
        nameof(Employee.Address),
        nameof(Employee.CompanyLocationId)
    };

    public async Task<Result> Handle(CreateProfileUpdateRequestCommand request, CancellationToken cancellationToken)
    {
        var employee = await _unitOfWork.Employees.GetByUserIdAsync(request.UserId, cancellationToken);
        if (employee is null) return Result.Failure(DomainErrors.Employee.NotFound);

        // Check for pending requests
        var hasPending =
            await _unitOfWork.ProfileUpdateRequests.ExistsAsync(
                r => r.EmployeeId == employee.Id && r.Status == ProfileUpdateRequestStatus.Pending, cancellationToken);
        if (hasPending)
            return Result.Failure(DomainErrors.ProfileUpdate.HasPending);

        // Validate fields and build JSON
        var changes = new Dictionary<string, object>();
        foreach (var pair in request.Dto.NewValues)
        {
            if (!AllowedFields.Contains(pair.Key))
                return Result.Failure(DomainErrors.ProfileUpdate.InvalidField);

            if (pair.Key == nameof(Employee.FullName) && string.IsNullOrWhiteSpace(pair.Value))
                return Result.Failure(DomainErrors.Validation.FieldRequired);

            var oldValue = GetValue(employee, pair.Key);

            // Check if the new value is actually different from the existing one
            var newValueString = pair.Value ?? string.Empty;
            var oldValueString = oldValue ?? string.Empty;

            if (!newValueString.Equals(oldValueString, StringComparison.OrdinalIgnoreCase))
            {
                changes.Add(pair.Key, new { oldValue, newValue = pair.Value });
            }
        }

        if (changes.Count == 0)
            return Result.Failure(DomainErrors.ProfileUpdate.NoChanges);

        var profileUpdateRequest = new ProfileUpdateRequest
        {
            EmployeeId = employee.Id,
            ChangesJson = JsonSerializer.Serialize(changes),
            Status = ProfileUpdateRequestStatus.Pending,
            EmployeeComment = request.Dto.Comment,
            CreatedById = request.UserId
        };

        await _unitOfWork.ProfileUpdateRequests.AddAsync(profileUpdateRequest, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private string? GetValue(Employee employee, string field)
    {
        return field switch
        {
            nameof(Employee.FullName) => employee.FullName,
            nameof(Employee.PhoneNumber) => employee.PhoneNumber,
            nameof(Employee.Address) => employee.Address,
            nameof(Employee.CompanyLocationId) => employee.CompanyLocationId?.ToString(),
            _ => null
        };
    }
}
