using HrSystemApp.Application.Common;

namespace HrSystemApp.Application.Interfaces.Services;

public interface IRequestSchemaValidator
{
    /// <summary>
    /// Validates the JSON data against the schema for the specified request type.
    /// </summary>
    Result Validate(string typeKey, string jsonData, string? customSchema = null);

    /// <summary>
    /// Returns the schema definition for a request type (either global or custom).
    /// </summary>
    object GetSchema(string typeKey, string? customSchema = null);
}
