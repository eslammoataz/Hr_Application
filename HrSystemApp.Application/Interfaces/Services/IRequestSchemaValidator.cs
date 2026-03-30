using HrSystemApp.Application.Common;
using HrSystemApp.Domain.Enums;

namespace HrSystemApp.Application.Interfaces.Services;

public interface IRequestSchemaValidator
{
    /// <summary>
    /// Validates the JSON data against the schema for the specified request type.
    /// </summary>
    Result Validate(RequestType type, string jsonData, string? customSchema = null);

    /// <summary>
    /// Returns the schema definition for a request type (either global or custom).
    /// </summary>
    object GetSchema(RequestType type, string? customSchema = null);
}
