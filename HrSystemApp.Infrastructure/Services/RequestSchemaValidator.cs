using System.Text.Json;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HrSystemApp.Infrastructure.Services;

public class RequestSchemaValidator : IRequestSchemaValidator, IDisposable
{
    private readonly JsonDocument _globalSchemas;
    private readonly ILogger<RequestSchemaValidator> _logger;
    private bool _disposed;

    public RequestSchemaValidator(IHostEnvironment environment, ILogger<RequestSchemaValidator> logger)
    {
        _logger = logger;

        // Primary path: Deployment (copied to output directory)
        var schemaPath = Path.Combine(AppContext.BaseDirectory, "Common", "RequestSchemas.json");

        // Fallback path: Local Development (points back to Application project)
        if (!File.Exists(schemaPath))
        {
            schemaPath = Path.Combine(environment.ContentRootPath, "..", "HrSystemApp.Application", "Common", "RequestSchemas.json");
        }

        if (File.Exists(schemaPath))
        {
            var jsonString = File.ReadAllText(schemaPath);
            _globalSchemas = JsonDocument.Parse(jsonString);
            _logger.LogInformation("Successfully loaded global request schemas from {SchemaPath}", schemaPath);
        }
        else
        {
            _globalSchemas = JsonDocument.Parse("{ \"Schemas\": {} }");
        }
    }

    public Result Validate(string typeKey, string jsonData, string? customSchema = null)
    {
        try
        {
            // First check if there's a custom schema for this specific request type (company override)
            if (!string.IsNullOrEmpty(customSchema))
            {
                var custom = JsonDocument.Parse(customSchema).RootElement;
                if (custom.TryGetProperty("properties", out _) || custom.ValueKind == JsonValueKind.Array)
                    return ValidateAgainstSchema(customSchema, typeKey, jsonData);
            }

            // Otherwise use the global schema from RequestSchemas.json
            if (_globalSchemas.RootElement.TryGetProperty("Schemas", out var schemas) &&
                schemas.TryGetProperty(typeKey, out var schemaElement))
            {
                return ValidateAgainstSchemaElement(schemaElement, typeKey, jsonData);
            }

            // No schema found — allow by default (no validation)
            _logger.LogWarning("No schema found for {RequestType}, skipping validation", typeKey);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during schema validation for {RequestType}", typeKey);
            return Result.Failure(DomainErrors.Validation.Error with { Message = $"Schema validation failed: {ex.Message}" });
        }
    }

    private Result ValidateAgainstSchema(string schemaJson, string typeKey, string jsonData)
    {
        try
        {
            var schemaElement = JsonDocument.Parse(schemaJson).RootElement;
            return ValidateAgainstSchemaElement(schemaElement, typeKey, jsonData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating custom schema for {RequestType}", typeKey);
            return Result.Failure(DomainErrors.Validation.Error with { Message = $"Schema validation failed: {ex.Message}" });
        }
    }

    private Result ValidateAgainstSchemaElement(JsonElement schemaElement, string typeKey, string jsonData)
    {
        using var dataDoc = JsonDocument.Parse(jsonData);
        var dataElement = dataDoc.RootElement;

        // Check if this is JSON Schema format (has "properties") or legacy flat format (has array items with "name")
        if (schemaElement.TryGetProperty("properties", out var properties))
        {
            return ValidateJsonSchema(schemaElement, dataElement, typeKey);
        }

        // Legacy flat format — iterate array of field descriptors
        foreach (var field in schemaElement.EnumerateArray())
        {
            var name = field.GetProperty("name").GetString()!;
            var isRequired = field.GetProperty("isRequired").GetBoolean();
            var expectedType = field.GetProperty("type").GetString();

            if (!dataElement.TryGetProperty(name, out var value))
            {
                if (isRequired)
                {
                    _logger.LogWarning("Validation failed for {RequestType}: Missing required field '{FieldName}'", typeKey, name);
                    return Result.Failure(DomainErrors.Validation.FieldRequired with { Message = $"Field '{name}' is required for {typeKey} requests." });
                }
                continue;
            }

            // Simple type check
            if (expectedType == "number" && value.ValueKind != JsonValueKind.Number)
                return Result.Failure(DomainErrors.Validation.InvalidType with { Message = $"Field '{name}' must be a number." });

            if (expectedType == "boolean" && value.ValueKind != JsonValueKind.True && value.ValueKind != JsonValueKind.False)
                return Result.Failure(DomainErrors.Validation.InvalidType with { Message = $"Field '{name}' must be a boolean." });

            if (expectedType == "string" && value.ValueKind != JsonValueKind.String)
            {
                _logger.LogWarning("Validation failed for {RequestType}: Field '{FieldName}' expected string but got {ActualType}", typeKey, name, value.ValueKind);
                return Result.Failure(DomainErrors.Validation.InvalidType with { Message = $"Field '{name}' must be a string." });
            }
        }

        return Result.Success();
    }

    private Result ValidateJsonSchema(JsonElement schema, JsonElement data, string typeKey)
    {
        // Check required fields
        if (schema.TryGetProperty("required", out var requiredElement))
        {
            foreach (var requiredField in requiredElement.EnumerateArray())
            {
                var fieldName = requiredField.GetString()!;
                if (!data.TryGetProperty(fieldName, out _))
                {
                    return Result.Failure(DomainErrors.Validation.FieldRequired with { Message = $"Field '{fieldName}' is required for {typeKey} requests." });
                }
            }
        }

        // If allowExtraFields is false, reject any fields not in schema
        // ( We'd need to pass this flag separately or infer it — for now, skip extra field check )

        // Validate each property
        if (schema.TryGetProperty("properties", out var properties))
        {
            foreach (var prop in properties.EnumerateObject())
            {
                var propName = prop.Name;
                if (!data.TryGetProperty(propName, out var dataValue))
                    continue; // optional field missing — that's fine

                var propSchema = prop.Value;
                var expectedType = propSchema.TryGetProperty("type", out var t) ? t.GetString() : null;

                if (expectedType == "string")
                {
                    if (dataValue.ValueKind != JsonValueKind.String)
                        return Result.Failure(DomainErrors.Validation.InvalidType with { Message = $"Field '{propName}' must be a string." });

                    // minLength check
                    if (propSchema.TryGetProperty("minLength", out var minLen))
                    {
                        if (dataValue.GetString()!.Length < minLen.GetInt32())
                            return Result.Failure(DomainErrors.Validation.Error with { Message = $"Field '{propName}' must be at least {minLen.GetInt32()} characters." });
                    }

                    // enum check
                    if (propSchema.TryGetProperty("enum", out var enumValues))
                    {
                        var val = dataValue.GetString()!;
                        var valid = enumValues.EnumerateArray().Any(e => e.GetString() == val);
                        if (!valid)
                            return Result.Failure(DomainErrors.Validation.Error with { Message = $"Field '{propName}' must be one of: {string.Join(", ", enumValues.EnumerateArray().Select(e => e.GetString()))}." });
                    }
                }
                else if (expectedType == "number")
                {
                    if (dataValue.ValueKind != JsonValueKind.Number)
                        return Result.Failure(DomainErrors.Validation.InvalidType with { Message = $"Field '{propName}' must be a number." });

                    if (propSchema.TryGetProperty("minimum", out var minVal))
                    {
                        if (dataValue.GetDouble() < minVal.GetDouble())
                            return Result.Failure(DomainErrors.Validation.Error with { Message = $"Field '{propName}' must be >= {minVal.GetDouble()}." });
                    }
                }
            }
        }

        return Result.Success();
    }

    public object GetSchema(string typeKey, string? customSchema = null)
    {
        if (!string.IsNullOrEmpty(customSchema))
            return JsonSerializer.Deserialize<object>(customSchema)!;

        if (_globalSchemas.RootElement.GetProperty("Schemas").TryGetProperty(typeKey, out var schema))
            return JsonSerializer.Deserialize<object>(schema.GetRawText())!;

        return new List<object>();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _globalSchemas?.Dispose();
            _disposed = true;
        }
    }
}
