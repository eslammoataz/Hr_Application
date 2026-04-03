using HrSystemApp.Application.Common;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace HrSystemApp.Application.Features.ContactAdmin.Commands.CreateContactAdminRequest;

public class CreateContactAdminRequestHandler : IRequestHandler<CreateContactAdminRequestCommand, Result>
{
    private readonly IUnitOfWork _unitOfWork;

    public CreateContactAdminRequestHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(CreateContactAdminRequestCommand request, CancellationToken cancellationToken)
    {
        var existsPending = await _unitOfWork.ContactAdminRequests.ExistsPendingRequestAsync(
            request.Email, request.CompanyName, request.PhoneNumber, cancellationToken);

        if (existsPending)
        {
            return Result.Failure(DomainErrors.ContactAdmin.DuplicatePendingRequest);
        }

        var emailTaken = await _unitOfWork.Users.GetByEmailAsync(request.Email, cancellationToken);
        if (emailTaken is not null)
        {
            return Result.Failure(DomainErrors.ContactAdmin.EmailAlreadyTaken);
        }

        var phoneTaken = await _unitOfWork.Users.GetByPhoneNumberAsync(request.PhoneNumber, cancellationToken);
        if (phoneTaken is not null)
        {
            return Result.Failure(DomainErrors.ContactAdmin.PhoneNumberAlreadyTaken);
        }

        var companyNameTaken =
            await _unitOfWork.Companies.ExistsAsync(c => c.CompanyName == request.CompanyName, cancellationToken);
        if (companyNameTaken)
        {
            return Result.Failure(DomainErrors.ContactAdmin.CompanyNameAlreadyTaken);
        }

        var contactRequest = new ContactAdminRequest
        {
            Name = request.Name,
            Email = request.Email,
            CompanyName = request.CompanyName,
            Role = UserRole.CompanyAdmin.ToString(),
            PhoneNumber = request.PhoneNumber
        };

        await _unitOfWork.ContactAdminRequests.AddAsync(contactRequest, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
