using FluentValidation;
using HrSystemApp.Application.Common.Logging;
using HrSystemApp.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HrSystemApp.Application.Behaviors;

public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;
    private readonly ILogger<ValidationBehavior<TRequest, TResponse>> _logger;
    private readonly LoggingOptions _loggingOptions;

    public ValidationBehavior(
        IEnumerable<IValidator<TRequest>> validators,
        ILogger<ValidationBehavior<TRequest, TResponse>> logger,
        IOptions<LoggingOptions> loggingOptions)
    {
        _validators = validators;
        _logger = logger;
        _loggingOptions = loggingOptions.Value;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var actionName = typeof(TRequest).Name;
        var requestId = request is IHaveRequestId hri ? hri.RequestId : (Guid?)null;

        if (!_validators.Any())
            return await next();

        _logger.LogDecision(_loggingOptions, actionName, LogStage.Validation,
            "ValidationStarted", new { ValidatorCount = _validators.Count() });

        var context = new ValidationContext<TRequest>(request);

        var validationResults = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = validationResults
            .Where(r => r.Errors.Count > 0)
            .SelectMany(r => r.Errors)
            .ToList();

        if (failures.Count > 0)
        {
            // Log FIELD NAMES only — never log error messages, they may contain user-supplied input (plan rule).
            var invalidFields = failures.Select(e => e.PropertyName).Distinct().ToList();
            _logger.LogDecision(_loggingOptions, actionName, LogStage.Validation,
                "ValidationFailed", new { InvalidFields = invalidFields, RequestId = requestId });

            // ValidationException is caught and mapped to 400 by ExceptionMiddleware —
            // this is intentional control flow, not an unhandled error.
            throw new ValidationException(failures);
        }

        _logger.LogDecision(_loggingOptions, actionName, LogStage.Validation,
            "ValidationPassed", new { RequestId = requestId });

        return await next();
    }
}
