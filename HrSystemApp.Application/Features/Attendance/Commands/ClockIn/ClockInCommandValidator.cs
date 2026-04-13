using FluentValidation;

namespace HrSystemApp.Application.Features.Attendance.Commands.ClockIn;

public class ClockInCommandValidator : AbstractValidator<ClockInCommand>
{
    public ClockInCommandValidator()
    {
        RuleFor(x => x.TimestampUtc)
            .Must(ts => ts == null || ts.Value <= DateTime.UtcNow.AddMinutes(5))
            .WithMessage("Clock-in timestamp cannot be in the future.");
    }
}
