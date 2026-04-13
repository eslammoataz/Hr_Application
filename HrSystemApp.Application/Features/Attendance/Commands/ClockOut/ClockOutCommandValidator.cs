using FluentValidation;

namespace HrSystemApp.Application.Features.Attendance.Commands.ClockOut;

public class ClockOutCommandValidator : AbstractValidator<ClockOutCommand>
{
    public ClockOutCommandValidator()
    {
        RuleFor(x => x.TimestampUtc)
            .Must(ts => ts == null || ts.Value <= DateTime.UtcNow.AddMinutes(5))
            .WithMessage("Clock-out timestamp cannot be in the future.");
    }
}
