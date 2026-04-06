namespace HrSystemApp.Application.Interfaces.Services;

public interface IAttendanceReminderService
{
    Task ProcessRemindersAsync(CancellationToken cancellationToken = default);
}
