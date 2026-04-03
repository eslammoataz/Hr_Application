using FluentValidation;
using HrSystemApp.Domain.Enums;

namespace HrSystemApp.Application.Features.Notifications.Commands.SendNotificationToEmployee;

public class SendNotificationToEmployeeCommandValidator : AbstractValidator<SendNotificationToEmployeeCommand>
{
    public SendNotificationToEmployeeCommandValidator()
    {
        RuleFor(x => x.EmployeeId)
            .NotEmpty().WithMessage("EmployeeId is required.");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(200).WithMessage("Title must not exceed 200 characters.");

        RuleFor(x => x.Message)
            .NotEmpty().WithMessage("Message is required.")
            .MaximumLength(2000).WithMessage("Message must not exceed 2000 characters.");

        RuleFor(x => x.Type)
            .IsInEnum().WithMessage("Invalid notification type.");
    }
}
