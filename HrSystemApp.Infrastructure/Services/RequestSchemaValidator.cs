using System.Text.Json;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Enums;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HrSystemApp.Infrastructure.Services;

public class RequestSchemaValidator : IRequestSchemaValidator
{
    private readonly JsonDocument _globalSchemas;
    private readonly ILogger<RequestSchemaValidator> _logger;

    public RequestSchemaValidator(IHostEnvironment environment, ILogger<RequestSchemaValidator> logger)
    {
        _logger = logger;
        var schemaPath = Path.Combine(environment.ContentRootPath, "..", "HrSystemApp.Application", "Common", "RequestSchemas.json");
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

    public Result Validate(RequestType type, string jsonData, string? customSchema = null)
    {
        try
        {
            var typeName = type.ToString();
            var schemaElement = !string.IsNullOrEmpty(customSchema) 
                ? JsonDocument.Parse(customSchema).RootElement 
                : _globalSchemas.RootElement.GetProperty("Schemas").GetProperty(typeName);

            using var dataDoc = JsonDocument.Parse(jsonData);
            var dataElement = dataDoc.RootElement;

            foreach (var field in schemaElement.EnumerateArray())
            {
                var name = field.GetProperty("name").GetString()!;
                var isRequired = field.GetProperty("isRequired").GetBoolean();
                var expectedType = field.GetProperty("type").GetString();

                if (!dataElement.TryGetProperty(name, out var value))
                {
                    if (isRequired)
                    {
                        _logger.LogWarning("Validation failed for {RequestType}: Missing required field '{FieldName}'", type, name);
                        return Result.Failure(new Error("Validation.FieldRequired", $"Field '{name}' is required for {type} requests."));
                    }
                    continue;
                }

                // Simple type check
                if (expectedType == "number" && value.ValueKind != JsonValueKind.Number)
                    return Result.Failure(new Error("Validation.InvalidType", $"Field '{name}' must be a number."));
                
                if (expectedType == "boolean" && value.ValueKind != JsonValueKind.True && value.ValueKind != JsonValueKind.False)
                    return Result.Failure(new Error("Validation.InvalidType", $"Field '{name}' must be a boolean."));

                // Date and String both come as strings/numbers in some cases, but simplified for now
                if (expectedType == "string" && value.ValueKind != JsonValueKind.String)
                {
                    _logger.LogWarning("Validation failed for {RequestType}: Field '{FieldName}' expected string but got {ActualType}", type, name, value.ValueKind);
                    return Result.Failure(new Error("Validation.InvalidType", $"Field '{name}' must be a string."));
                }
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during schema validation for {RequestType}", type);
            return Result.Failure(new Error("Validation.Error", $"Schema validation failed: {ex.Message}"));
        }
    }

    public object GetSchema(RequestType type, string? customSchema = null)
    {
        var typeName = type.ToString();
        if (!string.IsNullOrEmpty(customSchema))
            return JsonSerializer.Deserialize<object>(customSchema)!;

        if (_globalSchemas.RootElement.GetProperty("Schemas").TryGetProperty(typeName, out var schema))
            return JsonSerializer.Deserialize<object>(schema.GetRawText())!;

        return new List<object>();
    }
}
