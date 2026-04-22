using FluentValidation;
using HrSystemApp.Application.Resources;

namespace HrSystemApp.Application.Features.Attendance.Commands.ClockOut;

public class ClockOutCommandValidator : AbstractValidator<ClockOutCommand>
{
    public ClockOutCommandValidator()
    {
        RuleFor(x => x.TimestampUtc)
            .Must(ts => ts == null || ts.Value <= DateTime.UtcNow.AddMinutes(5))
            .WithErrorCode(ErrorCodes.ClockOutFutureTimestamp)
            .WithMessage(Messages.Validation.ClockOutFutureTimestamp);
    }
}
