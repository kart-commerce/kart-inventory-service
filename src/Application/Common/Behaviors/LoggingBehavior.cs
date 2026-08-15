using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace KartInventoryService.Application.Common.Behaviors;

/// <summary>
/// requirement-spec.md's Observability NFR row: every command/query gets a structured
/// Information log on completion, tagged with its own name and duration. Deliberately never logs
/// the request/response objects themselves (only the request's type name). Exceptions are
/// intentionally left unlogged here and rethrown as-is: they're logged once, at the true
/// boundary (the Api layer's GlobalExceptionHandler), never duplicated at every pipeline layer.
///
/// checkpoint-logging-standard.md's taxonomy stage 3 ("&lt;Command&gt;HandlerStarted", first line
/// inside Handle()) is generalized here rather than duplicated in every handler - this behavior
/// already wraps every MediatR request, so it's the one place that's true by construction instead
/// of by every handler author remembering to add it (mirrors kart-identity-service's
/// LoggingBehaviour exactly).
/// </summary>
public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "Stage {Stage}: {RequestName} handler started",
            $"{requestName}HandlerStarted",
            requestName);

        var response = await next();

        _logger.LogInformation(
            "Stage {Stage}: {RequestName} completed in {ElapsedMilliseconds}ms",
            $"{requestName}Completed",
            requestName,
            stopwatch.ElapsedMilliseconds);

        return response;
    }
}
