using System.Diagnostics;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.Common.Logging;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HrSystemApp.Application.Behaviors;

public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;
    private readonly LoggingOptions _loggingOptions;

    public LoggingBehavior(
        ILogger<LoggingBehavior<TRequest, TResponse>> logger,
        IOptions<LoggingOptions> loggingOptions)
    {
        _logger = logger;
        _loggingOptions = loggingOptions.Value;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_loggingOptions.EnableCommandPipelineLogging)
            return await next();

        var actionName = typeof(TRequest).Name;
        var sw = Stopwatch.StartNew();

        _logger.LogActionStart(_loggingOptions, actionName);

        // Await the handler — never wrap in try/catch/rethrow here.
        // Domain errors are captured as Result.Failure by handlers.
        // Infrastructure exceptions bubble up to ExceptionMiddleware naturally.
        var response = await next();

        sw.Stop();

        // Inspect the Result<T> without throwing.
        // All application handlers return Result or Result<T> which inherits from Result.
        var isFailure = response is Result r && r.IsFailure;

        if (isFailure)
        {
            var lastKnownState = new { RequestType = actionName };
            _logger.LogActionFailure(_loggingOptions, actionName, LogStage.Processing, lastKnownState);
        }
        else
        {
            _logger.LogActionSuccess(_loggingOptions, actionName, sw.ElapsedMilliseconds);
            _logger.LogSlowOperation(_loggingOptions, actionName, sw.ElapsedMilliseconds);
        }

        return response;
    }
}
