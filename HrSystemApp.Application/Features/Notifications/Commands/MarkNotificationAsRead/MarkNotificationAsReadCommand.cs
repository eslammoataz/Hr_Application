using HrSystemApp.Application.Common;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using MediatR;

namespace HrSystemApp.Application.Features.Notifications.Commands.MarkNotificationAsRead;

public record MarkNotificationAsReadCommand(Guid NotificationId) : IRequest<Result>;

public class MarkNotificationAsReadCommandHandler : IRequestHandler<MarkNotificationAsReadCommand, Result>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly INotificationService _notificationService;

    public MarkNotificationAsReadCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        INotificationService notificationService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _notificationService = notificationService;
    }

    public async Task<Result> Handle(MarkNotificationAsReadCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(userId))
            return Result.Failure(DomainErrors.Auth.Unauthorized);

        var employee = await _unitOfWork.Employees.GetByUserIdAsync(userId, cancellationToken);
        if (employee is null)
            return Result.Failure(DomainErrors.Employee.NotFound);

        var rowsAffected = await _notificationService.MarkAsReadAsync(request.NotificationId, employee.Id);
        if (rowsAffected == 0)
            return Result.Failure(DomainErrors.Notification.NotFound);

        return Result.Success();
    }
}
