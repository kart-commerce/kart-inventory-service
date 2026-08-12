using KartInventoryService.Api.Common;
using KartInventoryService.Api.Security;
using KartInventoryService.Application.Common.Models;
using KartInventoryService.Application.Features.GetOrderAllocations;
using KartInventoryService.Application.Features.GetStockLevel;
using KartInventoryService.Application.Features.ReleaseReservation;
using KartInventoryService.Application.Features.ReplenishStock;
using KartInventoryService.Application.Features.ReserveStock;
using Kart.Shared.Observability;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace KartInventoryService.Api.Controllers;

[ApiController]
[Route("v1/inventory")]
public sealed class InventoryController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ILogger<InventoryController> _logger;

    public InventoryController(ISender sender, ILogger<InventoryController> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    /// <summary>contracts/api-contract.yaml reserveStock - POST /v1/inventory/reserve (Order Service's saga step 1, RBAC-gated).</summary>
    [HttpPost("reserve")]
    [Authorize(Policy = AuthenticationExtensions.OrderServicePolicy)]
    [ProducesResponseType(typeof(ReservationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDto), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDto), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ReservationDto>> ReserveStock([FromBody] ReserveStockRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new ReserveStockCommand(request.OrderId, request.Sku, request.Qty), cancellationToken);
        return this.ToActionResult<ReservationDto, ReservationDto>(
            result,
            reservation => CreatedAtAction(nameof(GetStockLevel), new { sku = reservation.Sku }, reservation));
    }

    /// <summary>contracts/api-contract.yaml releaseReservation - POST /v1/inventory/release (idempotent, RBAC-gated).</summary>
    [HttpPost("release")]
    [Authorize(Policy = AuthenticationExtensions.OrderServicePolicy)]
    [ProducesResponseType(typeof(ReservationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDto), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReservationDto>> ReleaseReservation([FromBody] ReleaseReservationRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new ReleaseReservationCommand(request.ReservationId), cancellationToken);
        return this.ToActionResult<ReservationDto, ReservationDto>(result, reservation => Ok(reservation));
    }

    /// <summary>contracts/api-contract.yaml getStockLevel - GET /v1/inventory/{sku} (public read, CanRead unconditional).</summary>
    [HttpGet("{sku}")]
    [ProducesResponseType(typeof(StockLevelDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDto), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StockLevelDto>> GetStockLevel([FromRoute] string sku, [FromQuery] string? warehouseId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetStockLevelQuery(sku, warehouseId), cancellationToken);
        return this.ToActionResult<StockLevelDto, StockLevelDto>(result, stockLevel => Ok(stockLevel));
    }

    /// <summary>
    /// contracts/api-contract.yaml replenishStock - POST /v1/inventory/replenish. This repo's
    /// Implementation Addendum (see contracts/README.md): the upstream contract never defined a
    /// path for INV-8 despite requiring one.
    /// </summary>
    [HttpPost("replenish")]
    [Authorize(Policy = AuthenticationExtensions.ReplenishPolicy)]
    [ProducesResponseType(typeof(StockLevelDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDto), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StockLevelDto>> ReplenishStock([FromBody] ReplenishStockRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new ReplenishStockCommand(request.WarehouseId, request.Sku, request.QtyAdded), cancellationToken);
        return this.ToActionResult<StockLevelDto, StockLevelDto>(result, stockLevel => Ok(stockLevel));
    }

    /// <summary>
    /// Order Management (Admin) flow #7's "Assign Warehouse" view — read-only, called by
    /// kart-admin-service when rendering an order's detail screen. Warehouse allocation itself is
    /// fully automatic inside the reserve saga; this exposes the decision already made, it never
    /// lets an admin override it.
    /// </summary>
    [HttpGet("orders/{orderId:guid}/allocations")]
    [Authorize(Policy = AuthenticationExtensions.AdminOnlyPolicy)]
    [ProducesResponseType(typeof(IReadOnlyList<ReservationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ReservationDto>>> GetOrderAllocations([FromRoute] Guid orderId, CancellationToken cancellationToken)
    {
        using var _ = KartFlowContext.Push("OrderManagementAdmin");
        _logger.LogInformation("Stage {Stage}: order allocations requested for order {OrderId}", "InventoryAllocationLookupStarted", orderId);

        var result = await _sender.Send(new GetOrderAllocationsQuery(orderId), cancellationToken);

        _logger.LogInformation("Stage {Stage}: order allocations lookup completed for order {OrderId}", "InventoryAllocationLookupSucceeded", orderId);
        return this.ToActionResult<IReadOnlyList<ReservationDto>, IReadOnlyList<ReservationDto>>(result, list => Ok(list));
    }
}

/// <summary>contracts/api-contract.yaml reserveStock requestBody shape.</summary>
public sealed record ReserveStockRequest(Guid OrderId, string Sku, int Qty);

/// <summary>contracts/api-contract.yaml releaseReservation requestBody shape.</summary>
public sealed record ReleaseReservationRequest(Guid ReservationId);

/// <summary>contracts/api-contract.yaml replenishStock requestBody shape.</summary>
public sealed record ReplenishStockRequest(string WarehouseId, string Sku, int QtyAdded);
