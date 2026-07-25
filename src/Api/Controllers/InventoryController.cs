using KartInventoryService.Api.Common;
using KartInventoryService.Api.Security;
using KartInventoryService.Application.Common.Models;
using KartInventoryService.Application.Features.GetStockLevel;
using KartInventoryService.Application.Features.ReleaseReservation;
using KartInventoryService.Application.Features.ReplenishStock;
using KartInventoryService.Application.Features.ReserveStock;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KartInventoryService.Api.Controllers;

[ApiController]
[Route("v1/inventory")]
public sealed class InventoryController : ControllerBase
{
    private readonly ISender _sender;

    public InventoryController(ISender sender)
    {
        _sender = sender;
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
}

/// <summary>contracts/api-contract.yaml reserveStock requestBody shape.</summary>
public sealed record ReserveStockRequest(Guid OrderId, string Sku, int Qty);

/// <summary>contracts/api-contract.yaml releaseReservation requestBody shape.</summary>
public sealed record ReleaseReservationRequest(Guid ReservationId);

/// <summary>contracts/api-contract.yaml replenishStock requestBody shape.</summary>
public sealed record ReplenishStockRequest(string WarehouseId, string Sku, int QtyAdded);
