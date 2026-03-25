using HrSystemApp.Application.Common;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using HrSystemApp.Application.Features.Companies.Commands.CreateCompany;
using HrSystemApp.Application.Features.Employees.Commands.CreateEmployee;
using MediatR;

namespace HrSystemApp.Application.Features.ContactAdmin.Commands.AcceptContactAdminRequest;

public class AcceptContactAdminRequestHandler : IRequestHandler<AcceptContactAdminRequestCommand, Result>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmailService _emailService;
    private readonly ISender _sender;

    public AcceptContactAdminRequestHandler(IUnitOfWork unitOfWork, IEmailService emailService, ISender sender)
    {
        _unitOfWork = unitOfWork;
        _emailService = emailService;
        _sender = sender;
    }

    public async Task<Result> Handle(AcceptContactAdminRequestCommand request, CancellationToken cancellationToken)
    {
        // 1. Get contact request
        var contactRequest = await _unitOfWork.ContactAdminRequests.GetByIdAsync(request.Id, cancellationToken);

        if (contactRequest == null)
            return Result.Failure(DomainErrors.ContactAdmin.NotFound);

        if (contactRequest.Status != ContactAdminRequestStatus.Pending)
            return Result.Failure(DomainErrors.ContactAdmin.AlreadyProcessed);

        var userRole = Enum.Parse<UserRole>(contactRequest.Role);

        // 3. Create Company
        var companyResult = await _sender.Send(
            new CreateCompanyCommand(contactRequest.CompanyName, null, 21),
            cancellationToken);

        if (companyResult.IsFailure)
            return Result.Failure(companyResult.Error);

        // 4. Create Employee with parsed role
        var employeeResult = await _sender.Send(
            new CreateEmployeeCommand(
                contactRequest.Name,
                contactRequest.Email,
                contactRequest.PhoneNumber,
                companyResult.Value.Id,
                userRole),
            cancellationToken);

        if (employeeResult.IsFailure)
            return Result.Failure(employeeResult.Error);

        // 5. Mark request as Accepted
        contactRequest.Status = ContactAdminRequestStatus.Accepted;
        await _unitOfWork.ContactAdminRequests.UpdateAsync(contactRequest, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // 6. Send Welcome Email
        await _emailService.SendWelcomeEmailAsync(
            contactRequest.Email,
            contactRequest.Name,
            contactRequest.CompanyName,
            employeeResult.Value.TemporaryPassword,
            cancellationToken);

        return Result.Success();
    }
}