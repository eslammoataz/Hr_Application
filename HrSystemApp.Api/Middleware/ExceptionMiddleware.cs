using System.Net;
using System.Text.Json;
using FluentValidation;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Common.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace HrSystemApp.Api.Middleware;

/// <summary>
/// Global exception middleware that captures unhandled errors and returns Result-compatible responses.
/// </summary>
public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;
    private readonly LoggingOptions _loggingOptions;
    private readonly IHostEnvironment _env;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger, IHostEnvironment env, IOptions<LoggingOptions> loggingOptions)
    {
        _next = next;
        _logger = logger;
        _env = env;
        _loggingOptions = loggingOptions.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            var userId = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "Anonymous";
            var correlationId = context.Response.Headers["X-Correlation-ID"].ToString();
            var lastKnownState = new { Path = context.Request.Path.Value, Method = context.Request.Method };

            _logger.LogActionFailure(
                _loggingOptions,
                "UnhandledException",
                LogStage.Processing,
                ex,
                lastKnownState);

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
                DomainErrors.General.ValidationError with { Message = GetValidationMessage(validationEx) }
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
                DomainErrors.General.ArgumentError with { Message = argEx.Message }
            ),
            InvalidOperationException opEx => (
                HttpStatusCode.BadRequest,
                DomainErrors.General.InvalidOperation with { Message = opEx.Message }
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
            .ToList();

        return string.Join("; ", messages);
    }
}