using KartInventoryService.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace KartInventoryService.Api.Common;

/// <summary>
/// Translates a Handler's Result&lt;T&gt; failure (api-standards.md: "Domain/business errors use a
/// Result/Either pattern - not exceptions") into the HTTP status/Problem shape
/// contracts/api-contract.yaml specifies per endpoint.
/// </summary>
public static class ResultExtensions
{
    public static ActionResult<TResponse> ToActionResult<TValue, TResponse>(
        this ControllerBase controller,
        Result<TValue> result,
        Func<TValue, ActionResult<TResponse>> onSuccess)
    {
        return result.IsSuccess ? onSuccess(result.Value) : controller.MapFailure<TResponse>(result.Error);
    }

    public static ActionResult MapFailure(this ControllerBase controller, Error error)
    {
        var problem = new ProblemDto(error.Code, error.Message);
        return error.Code switch
        {
            "not_found" => controller.NotFound(problem),
            "validation_error" => controller.BadRequest(problem),
            "insufficient_stock" => controller.Conflict(problem),
            "lock_timeout" => controller.StatusCode(StatusCodes.Status503ServiceUnavailable, problem),
            _ => controller.StatusCode(StatusCodes.Status500InternalServerError, problem),
        };
    }

    private static ActionResult<TResponse> MapFailure<TResponse>(this ControllerBase controller, Error error) =>
        new(controller.MapFailure(error));
}
