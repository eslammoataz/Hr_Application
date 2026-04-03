namespace HrSystemApp.Domain.Enums;

public enum NotificationType
{
    Request = 0,
    RequestApproved = 1,
    RequestRejected = 2,
    RequestPending = 3,
    TaskAssignment = 4,
    TaskReminder = 5,
    SurveyForwarded = 6,
    SurveyCompleted = 7,
    ComplaintUpdate = 8,
    Announcement = 9,
    LeaveBalanceWarning = 10,
    AttendanceReminder = 11,
    General = 12
}
