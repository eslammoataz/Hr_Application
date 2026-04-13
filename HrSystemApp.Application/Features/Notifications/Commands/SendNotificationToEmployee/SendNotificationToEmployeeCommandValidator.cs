using FluentValidation;
using HrSystemApp.Application.Resources;
using HrSystemApp.Domain.Enums;

namespace HrSystemApp.Application.Features.Notifications.Commands.SendNotificationToEmployee;

public class SendNotificationToEmployeeCommandValidator : AbstractValidator<SendNotificationToEmployeeCommand>
{
    public SendNotificationToEmployeeCommandValidator()
    {
        RuleFor(x => x.EmployeeId)
            .NotEmpty().WithMessage(Messages.Validation.EmployeeIdRequired);

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage(Messages.Validation.TitleRequired)
            .MaximumLength(200).WithMessage(Messages.Validation.TitleMaxLength);

        RuleFor(x => x.Message)
            .NotEmpty().WithMessage(Messages.Validation.MessageRequired)
            .MaximumLength(2000).WithMessage(Messages.Validation.MessageMaxLength);

        RuleFor(x => x.Type)
            .IsInEnum().WithMessage(Messages.Validation.InvalidNotificationType);
    }
}
