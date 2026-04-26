using FluentValidation;
using HrSystemApp.Application.Features.RequestTypes.Commands;

namespace HrSystemApp.Application.Features.RequestTypes.Commands;

public class CreateRequestTypeCommandValidator : AbstractValidator<CreateRequestTypeCommand>
{
    public CreateRequestTypeCommandValidator()
    {
        RuleFor(x => x.KeyName)
            .NotEmpty().WithMessage("KeyName is required.")
            .Matches(@"^[a-z][a-z0-9]*$").WithMessage("KeyName must be lowercase alphanumeric, starting with a letter.");

        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage("DisplayName is required.")
            .MaximumLength(200).WithMessage("DisplayName must not exceed 200 characters.");

        RuleFor(x => x.FormSchemaJson)
            .Must(BeValidJson).When(x => !string.IsNullOrEmpty(x.FormSchemaJson))
            .WithMessage("FormSchemaJson must be valid JSON.");

        RuleFor(x => x.RequestNumberPattern)
            .MaximumLength(100).WithMessage("RequestNumberPattern must not exceed 100 characters.");

        RuleFor(x => x.DefaultSlaDays)
            .GreaterThan(0).When(x => x.DefaultSlaDays.HasValue)
            .WithMessage("DefaultSlaDays must be a positive number.");

        RuleFor(x => x.DisplayNameLocalizationsJson)
            .Must(BeValidJson).When(x => !string.IsNullOrEmpty(x.DisplayNameLocalizationsJson))
            .WithMessage("DisplayNameLocalizationsJson must be valid JSON.");
    }

    private static bool BeValidJson(string? json)
    {
        if (string.IsNullOrEmpty(json)) return true;
        try
        {
            System.Text.Json.JsonDocument.Parse(json);
            return true;
        }
        catch
        {
            return false;
        }
    }
}