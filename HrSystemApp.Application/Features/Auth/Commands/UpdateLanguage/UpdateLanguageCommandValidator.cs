using FluentValidation;
using HrSystemApp.Application.Resources;

namespace HrSystemApp.Application.Features.Auth.Commands.UpdateLanguage;

public class UpdateLanguageCommandValidator : AbstractValidator<UpdateLanguageCommand>
{
    public UpdateLanguageCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithErrorCode(ErrorCodes.FieldRequired).WithMessage(Messages.Validation.FieldRequired);

        RuleFor(x => x.Language)
            .NotEmpty().WithErrorCode(ErrorCodes.LanguageNotEmpty).WithMessage(Messages.Validation.LanguageNotEmpty)
            .MaximumLength(10).WithErrorCode(ErrorCodes.LanguageMaxLength).WithMessage(Messages.Validation.LanguageMaxLength);
    }
}
