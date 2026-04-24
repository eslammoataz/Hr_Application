using System.Text.Json;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Enums;
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
                        return Result.Failure(DomainErrors.Validation.FieldRequired with { Message = $"Field '{name}' is required for {type} requests." });
                    }
                    continue;
                }

                // Simple type check
                if (expectedType == "number" && value.ValueKind != JsonValueKind.Number)
                    return Result.Failure(DomainErrors.Validation.InvalidType with { Message = $"Field '{name}' must be a number." });
                
                if (expectedType == "boolean" && value.ValueKind != JsonValueKind.True && value.ValueKind != JsonValueKind.False)
                    return Result.Failure(DomainErrors.Validation.InvalidType with { Message = $"Field '{name}' must be a boolean." });

                // Date and String both come as strings/numbers in some cases, but simplified for now
                if (expectedType == "string" && value.ValueKind != JsonValueKind.String)
                {
                    _logger.LogWarning("Validation failed for {RequestType}: Field '{FieldName}' expected string but got {ActualType}", type, name, value.ValueKind);
                    return Result.Failure(DomainErrors.Validation.InvalidType with { Message = $"Field '{name}' must be a string." });
                }
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during schema validation for {RequestType}", type);
            return Result.Failure(DomainErrors.Validation.Error with { Message = $"Schema validation failed: {ex.Message}" });
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

    public void Dispose()
    {
        if (!_disposed)
        {
            _globalSchemas?.Dispose();
            _disposed = true;
        }
    }
}
