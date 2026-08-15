using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace KartInventoryService.Application.Common.Behaviors;

/// <summary>
/// Runs every registered FluentValidation validator for a request before its Handler executes
/// (api-standards.md: "Input validated at the API boundary"). A request type with no registered
/// validator passes through untouched.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;
    private readonly ILogger<ValidationBehavior<TRequest, TResponse>> _logger;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators, ILogger<ValidationBehavior<TRequest, TResponse>> logger)
    {
        _validators = validators;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (_validators.Any())
        {
            var context = new ValidationContext<TRequest>(request);
            var failures = (await Task.WhenAll(_validators.Select(v => v.ValidateAsync(context, cancellationToken))))
                .SelectMany(result => result.Errors)
                .Where(failure => failure is not null)
                .ToList();

            if (failures.Count > 0)
            {
                var requestName = typeof(TRequest).Name;

                _logger.LogWarning(
                    "Stage {Stage}: {RequestName} rejected - {Errors}",
                    $"{requestName}ValidationFailed",
                    requestName,
                    string.Join("; ", failures.Select(f => $"{f.PropertyName}: {f.ErrorMessage}")));

                throw new ValidationException(failures);
            }
        }

        return await next();
    }
}
