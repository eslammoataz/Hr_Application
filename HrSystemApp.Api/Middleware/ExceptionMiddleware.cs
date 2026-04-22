using System.Net;
using System.Text.Json;
using FluentValidation;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Common.Logging;
using HrSystemApp.Application.Resources;
using Microsoft.Extensions.Localization;
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

    public ExceptionMiddleware(
        RequestDelegate next,
        ILogger<ExceptionMiddleware> logger,
        IHostEnvironment env,
        IOptions<LoggingOptions> loggingOptions)
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

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        // Resolve scoped services from the current request scope
        var localizer = context.RequestServices.GetRequiredService<IErrorLocalizer>();
        var validationLocalizer = context.RequestServices.GetRequiredService<IStringLocalizer<ValidationMessages>>();

        var (statusCode, error) = MapExceptionToError(exception, validationLocalizer);
        var localizedError = localizer.Localize(error);

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var response = new ApiResponse<object>(false, null, localizedError);
        await context.Response.WriteAsync(JsonSerializer.Serialize(response, JsonOptions));
    }

    private (HttpStatusCode statusCode, Error error) MapExceptionToError(Exception exception, IStringLocalizer<ValidationMessages> validationLocalizer)
    {
        return exception switch
        {
            ValidationException validationEx => (
                HttpStatusCode.BadRequest,
                DomainErrors.General.ValidationError with { Message = GetValidationMessage(validationEx, validationLocalizer) }
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

    private static string GetValidationMessage(ValidationException validationEx, IStringLocalizer<ValidationMessages> localizer)
    {
        var messages = validationEx.Errors.Select(e =>
        {
            if (!string.IsNullOrEmpty(e.ErrorCode))
            {
                var localized = localizer[e.ErrorCode];
                if (!localized.ResourceNotFound)
                    return localized.Value;
            }
            return e.ErrorMessage;
        });

        return string.Join("; ", messages);
    }
}
