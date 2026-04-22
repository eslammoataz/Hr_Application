using HrSystemApp.Application.Common;

namespace HrSystemApp.Application.Resources;

/// <summary>
/// Translates an Error's message into the current request culture.
/// Uses Error.Code as the lookup key in the resource files.
/// If no translation exists for a key, falls back to the English Error.Message.
/// </summary>
public interface IErrorLocalizer
{
    /// <summary>
    /// Returns a copy of the error with its Message replaced by the localized version.
    /// The Code is always preserved unchanged.
    /// </summary>
    Error Localize(Error error);
}
