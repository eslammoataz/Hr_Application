using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Notifications;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using MediatR;

namespace HrSystemApp.Application.Features.Notifications.Queries.GetMyNotifications;

public record GetMyNotificationsQuery : IRequest<Result<List<NotificationResponse>>>;

public class GetMyNotificationsQueryHandler : IRequestHandler<GetMyNotificationsQuery, Result<List<NotificationResponse>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly INotificationService _notificationService;

    public GetMyNotificationsQueryHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        INotificationService notificationService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _notificationService = notificationService;
    }

    public async Task<Result<List<NotificationResponse>>> Handle(GetMyNotificationsQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(userId))
            return Result.Failure<List<NotificationResponse>>(DomainErrors.Auth.Unauthorized);

        var employee = await _unitOfWork.Employees.GetByUserIdAsync(userId, cancellationToken);
        if (employee is null)
            return Result.Failure<List<NotificationResponse>>(DomainErrors.Employee.NotFound);

        var notifications = await _notificationService.GetUserNotifications(employee.Id);
        var response = notifications
            .Select(n => new NotificationResponse(
                n.Id,
                n.EmployeeId,
                n.Title,
                n.Message,
                n.Type,
                n.IsRead,
                n.CreatedAt))
            .ToList();

        return Result.Success(response);
    }
}
