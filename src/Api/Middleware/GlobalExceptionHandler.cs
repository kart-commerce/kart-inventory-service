using FluentValidation;
using KartInventoryService.Api.Common;
using Microsoft.AspNetCore.Diagnostics;

namespace KartInventoryService.Api.Middleware;

/// <summary>
/// The single place every exception reaching the HTTP boundary is logged and translated to
/// contracts/api-contract.yaml's Problem shape (ProblemDto) - handlers/controllers never need
/// their own try/catch (design-decisions.md, "Global Exception Handling & Consistent Response
/// Model"). FluentValidation's ValidationException (thrown by ValidationBehavior, never caught
/// anywhere in the MediatR pipeline by design) maps to 400; every other exception is a genuine,
/// unanticipated failure and maps to a generic 500 that never leaks exception internals to the
/// client - the full exception (message, stack trace) only ever reaches the structured log,
/// logged exactly once here, never duplicated at any other layer.
/// </summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is ValidationException validationException)
        {
            logger.LogWarning(
                exception,
                "Request rejected with validation_error (400) for {Method} {Path}",
                httpContext.Request.Method,
                httpContext.Request.Path);

            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await httpContext.Response.WriteAsJsonAsync(ToValidationProblem(validationException), cancellationToken);
            return true;
        }

        // Not a recognized/expected exception type - a genuine unhandled failure at the API
        // boundary. Logged at Error (not Warning) since this always represents a bug or an
        // unhandled infrastructure fault, never an expected client-facing rejection.
        logger.LogError(
            exception,
            "Unhandled exception for {Method} {Path}",
            httpContext.Request.Method,
            httpContext.Request.Path);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(
            new ProblemDto("internal_error", "An unexpected error occurred."),
            cancellationToken);
        return true;
    }

    private static ProblemDto ToValidationProblem(ValidationException exception)
    {
        var details = exception.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, object? (g) => g.Select(e => e.ErrorMessage).ToArray());

        return new ProblemDto("validation_error", "One or more validation errors occurred.", details);
    }
}
