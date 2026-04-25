using HrSystemApp.Application.Common;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Application.Features.Companies.Commands.CreateCompany;
using HrSystemApp.Application.Features.Employees.Commands.CreateEmployee;
using MediatR;
using HrSystemApp.Application.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace HrSystemApp.Application.Features.ContactAdmin.Commands.AcceptContactAdminRequest;

public class AcceptContactAdminRequestHandler : IRequestHandler<AcceptContactAdminRequestCommand, Result>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmailService _emailService;

    private readonly ISender _sender;
    private readonly ILogger<AcceptContactAdminRequestHandler> _logger;

    public AcceptContactAdminRequestHandler(IUnitOfWork unitOfWork, IEmailService emailService, ISender sender, ILogger<AcceptContactAdminRequestHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _emailService = emailService;

        _sender = sender;
        _logger = logger;
    }

    public async Task<Result> Handle(AcceptContactAdminRequestCommand request, CancellationToken cancellationToken)
    {
        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            // 1. Get contact request
            var contactRequest = await _unitOfWork.ContactAdminRequests.GetByIdAsync(request.Id, cancellationToken);

            if (contactRequest == null)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result.Failure(DomainErrors.ContactAdmin.NotFound);
            }

            if (contactRequest.Status != ContactAdminRequestStatus.Pending)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result.Failure(DomainErrors.ContactAdmin.AlreadyProcessed);
            }

            var userRole = Enum.Parse<UserRole>(contactRequest.Role);

            // 2. Create Company
            var companyResult = await _sender.Send(
                new CreateCompanyCommand(
                    contactRequest.CompanyName,
                    null,
                    21,
                    new TimeSpan(9, 0, 0),
                    new TimeSpan(17, 0, 0),
                    15,
                    "UTC"),
                cancellationToken);

            if (companyResult.IsFailure)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result.Failure(companyResult.Error);
            }

            // 3. Create Employee with parsed role
            var employeeResult = await _sender.Send(
                new CreateEmployeeCommand(
                    contactRequest.Name,
                    contactRequest.Email,
                    contactRequest.PhoneNumber,
                    companyResult.Value.Id,
                    userRole),
                cancellationToken);

            if (employeeResult.IsFailure)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result.Failure(employeeResult.Error);
            }

            // 4. Mark request as Accepted
            contactRequest.Status = ContactAdminRequestStatus.Accepted;
            await _unitOfWork.ContactAdminRequests.UpdateAsync(contactRequest, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            _ = _emailService.SendWelcomeEmailAsync(
                contactRequest.Email,
                contactRequest.Name,
                contactRequest.CompanyName,
                contactRequest.PhoneNumber,
                cancellationToken);

            return Result.Success();
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            _logger.LogError(ex, "AcceptContactAdminRequest failed for request {Id}", request.Id);
            return Result.Failure(new Error("General.ServerError", ex.Message));
        }
    }
}
