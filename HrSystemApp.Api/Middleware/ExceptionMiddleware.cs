using System.Net;
using System.Text.Json;
using FluentValidation;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.Errors;

namespace HrSystemApp.Api.Middleware;

/// <summary>
/// Global exception middleware that captures unhandled errors and returns Result-compatible responses.
/// </summary>
public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;
    private readonly IHostEnvironment _env;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger, IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, error) = MapExceptionToError(exception);
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var response = new ApiResponse<object>(false, null, error);
        return context.Response.WriteAsync(JsonSerializer.Serialize(response, JsonOptions));
    }

    private static (HttpStatusCode statusCode, Error error) MapExceptionToError(Exception exception)
    {
        return exception switch
        {
            ValidationException validationEx => (
                HttpStatusCode.BadRequest,
                new Error(DomainErrors.General.ValidationError.Code, GetValidationMessage(validationEx))
            ),
            UnauthorizedAccessException => (
                HttpStatusCode.Unauthorized,
                DomainErrors.Auth.Unauthorized
            ),
            KeyNotFoundException or FileNotFoundException => (
                HttpStatusCode.NotFound,
                DomainErrors.General.NotFound
            ),
            ArgumentException argEx => (
                HttpStatusCode.BadRequest,
                new Error("General.ArgumentError", argEx.Message)
            ),
            InvalidOperationException opEx => (
                HttpStatusCode.BadRequest,
                new Error("General.InvalidOperation", opEx.Message)
            ),
            _ => (
                HttpStatusCode.InternalServerError,
                DomainErrors.General.ServerError
            )
        };
    }

    private static string GetValidationMessage(ValidationException validationEx)
    {
        var messages = validationEx.Errors
            .Select(e => e.ErrorMessage)
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Distinct();

        return messages.Any()
            ? string.Join(" ", messages)
            : DomainErrors.General.ValidationError.Message;
    }

}
