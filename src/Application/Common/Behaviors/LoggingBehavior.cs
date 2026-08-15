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

        var response = await next();

        _logger.LogInformation(
            "{RequestName} completed in {ElapsedMilliseconds}ms",
            requestName,
            stopwatch.ElapsedMilliseconds);

        return response;
    }
}
