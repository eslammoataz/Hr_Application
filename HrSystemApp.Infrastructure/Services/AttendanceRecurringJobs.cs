using HrSystemApp.Application.Interfaces.Services;

namespace HrSystemApp.Infrastructure.Services;

public class AttendanceRecurringJobs
{
    private readonly IAttendanceReminderService _reminderService;
    private readonly IAutoClockOutService _autoClockOutService;

    public AttendanceRecurringJobs(
        IAttendanceReminderService reminderService,
        IAutoClockOutService autoClockOutService)
    {
        _reminderService = reminderService;
        _autoClockOutService = autoClockOutService;
    }

    public Task RunReminderJob()
    {
        return _reminderService.ProcessRemindersAsync();
    }

    public Task RunAutoClockOutJob()
    {
        return _autoClockOutService.ProcessAutoClockOutAsync();
    }
}
