using FluentValidation;
using HrSystemApp.Application.Resources;

namespace HrSystemApp.Application.Features.Auth.Commands.UpdateLanguage;

public class UpdateLanguageCommandValidator : AbstractValidator<UpdateLanguageCommand>
{
    public UpdateLanguageCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage(Messages.Validation.FieldRequired);

        RuleFor(x => x.Language)
            .NotEmpty().WithMessage(Messages.Validation.LanguageNotEmpty)
            .MaximumLength(10).WithMessage(Messages.Validation.LanguageMaxLength);
    }
}
