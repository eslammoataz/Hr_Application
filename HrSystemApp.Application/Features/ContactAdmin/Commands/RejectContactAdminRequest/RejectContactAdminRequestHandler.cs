using HrSystemApp.Application.Common;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Domain.Enums;
using MediatR;

namespace HrSystemApp.Application.Features.ContactAdmin.Commands.RejectContactAdminRequest;

public class RejectContactAdminRequestHandler : IRequestHandler<RejectContactAdminRequestCommand, Result>
{
    private readonly IUnitOfWork _unitOfWork;

    public RejectContactAdminRequestHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(RejectContactAdminRequestCommand request, CancellationToken cancellationToken)
    {
        var contactRequest = await _unitOfWork.ContactAdminRequests.GetByIdAsync(request.Id, cancellationToken);

        if (contactRequest == null)
        {
            return Result.Failure(DomainErrors.ContactAdmin.NotFound);
        }

        if (contactRequest.Status != ContactAdminRequestStatus.Pending)
        {
            return Result.Failure(DomainErrors.ContactAdmin.AlreadyProcessed);
        }

        contactRequest.Status = ContactAdminRequestStatus.Rejected;
        
        await _unitOfWork.ContactAdminRequests.UpdateAsync(contactRequest, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
