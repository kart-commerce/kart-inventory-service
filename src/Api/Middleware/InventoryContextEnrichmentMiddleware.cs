using Serilog.Context;

namespace KartInventoryService.Api.Middleware;

/// <summary>
/// requirement-spec.md's Observability NFR row: structured logs carry `sku` alongside the
/// mandatory `traceId`/`service`/`level` fields. The route value is present on GET
/// /inventory/{sku}; a no-op for the write endpoints (reserve/release/replenish), whose
/// correlating identifiers live in the request body, not the route - mirrors
/// kart-category-service's CategoryContextEnrichmentMiddleware.
/// </summary>
public sealed class InventoryContextEnrichmentMiddleware
{
    private const string SkuRouteValueKey = "sku";

    private readonly RequestDelegate _next;

    public InventoryContextEnrichmentMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.RouteValues.TryGetValue(SkuRouteValueKey, out var sku) || sku is null)
        {
            await _next(context);
            return;
        }

        using (LogContext.PushProperty("sku", sku))
        {
            await _next(context);
        }
    }
}
