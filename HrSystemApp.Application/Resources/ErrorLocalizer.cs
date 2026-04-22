using HrSystemApp.Application.Common;
using Microsoft.Extensions.Localization;

namespace HrSystemApp.Application.Resources;

public sealed class ErrorLocalizer : IErrorLocalizer
{
    private readonly IStringLocalizer<ErrorMessages> _localizer;

    public ErrorLocalizer(IStringLocalizer<ErrorMessages> localizer)
    {
        _localizer = localizer;
    }

    public Error Localize(Error error)
    {
        if (error == Error.None)
            return error;

        var localized = _localizer[error.Code];

        // ResourceNotFound = true means no entry exists in the resx for this key.
        // Fall back to the original English Message so nothing breaks.
        if (localized.ResourceNotFound)
            return error;

        return error with { Message = localized.Value };
    }
}
