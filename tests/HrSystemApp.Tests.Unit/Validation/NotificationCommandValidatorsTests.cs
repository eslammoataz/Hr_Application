using FluentAssertions;
using HrSystemApp.Application.Features.Notifications.Commands.BroadcastNotification;
using HrSystemApp.Application.Features.Notifications.Commands.SendNotificationToEmployee;
using HrSystemApp.Domain.Enums;

namespace HrSystemApp.Tests.Unit.Validation;

public class NotificationCommandValidatorsTests
{
    [Fact]
    public void BroadcastNotification_EmptyTitle_ShouldFail()
    {
        var validator = new BroadcastNotificationCommandValidator();
        var command = new BroadcastNotificationCommand("", "Body", NotificationType.General);

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.PropertyName == "Title");
    }

    [Fact]
    public void SendNotificationToEmployee_EmptyEmployeeId_ShouldFail()
    {
        var validator = new SendNotificationToEmployeeCommandValidator();
        var command = new SendNotificationToEmployeeCommand(Guid.Empty, "Title", "Body", NotificationType.General);

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.PropertyName == "EmployeeId");
    }
}
