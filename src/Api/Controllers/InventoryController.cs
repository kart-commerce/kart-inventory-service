using KartInventoryService.Api.Common;
using KartInventoryService.Api.Security;
using KartInventoryService.Application.Common.Models;
using KartInventoryService.Application.Features.GetLowStock;
using KartInventoryService.Application.Features.GetOrderAllocations;
using KartInventoryService.Application.Features.GetStockLevel;
using KartInventoryService.Application.Features.ProvisionWarehouseStock;
using KartInventoryService.Application.Features.ReconcileStock;
using KartInventoryService.Application.Features.ReleaseReservation;
using KartInventoryService.Application.Features.ReplenishStock;
using KartInventoryService.Application.Features.ReserveStock;
using KartInventoryService.Application.Features.UpdateReplenishmentThreshold;
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
    private const string FlowName = "InventoryStockManagement";

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
        using var _ = KartFlowContext.Push(FlowName);
        _logger.LogInformation("Stage {Stage}: reserve received for order {OrderId}, sku {Sku}.", "InventoryReserveRequested", request.OrderId, request.Sku);

        _logger.LogInformation("Stage {Stage}: dispatching ReserveStockCommand for order {OrderId}, sku {Sku}.", "ReserveStockCommandDispatched", request.OrderId, request.Sku);
        var result = await _sender.Send(new ReserveStockCommand(request.OrderId, request.Sku, request.Qty), cancellationToken);

        if (result.IsSuccess)
        {
            _logger.LogInformation("Stage {Stage}: order {OrderId} reserve request completed successfully.", "InventoryStockManagementProcessCompletedSuccessfully", request.OrderId);
        }

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
        using var _ = KartFlowContext.Push(FlowName);
        _logger.LogInformation("Stage {Stage}: release received for reservation {ReservationId}.", "InventoryReleaseRequested", request.ReservationId);

        _logger.LogInformation("Stage {Stage}: dispatching ReleaseReservationCommand for reservation {ReservationId}.", "ReleaseReservationCommandDispatched", request.ReservationId);
        var result = await _sender.Send(new ReleaseReservationCommand(request.ReservationId), cancellationToken);

        if (result.IsSuccess)
        {
            _logger.LogInformation("Stage {Stage}: reservation {ReservationId} release request completed successfully.", "InventoryStockManagementProcessCompletedSuccessfully", request.ReservationId);
        }

        return this.ToActionResult<ReservationDto, ReservationDto>(result, reservation => Ok(reservation));
    }

    /// <summary>contracts/api-contract.yaml getStockLevel - GET /v1/inventory/{sku} (public read, CanRead unconditional).</summary>
    [HttpGet("{sku}")]
    [ProducesResponseType(typeof(StockLevelDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDto), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StockLevelDto>> GetStockLevel([FromRoute] string sku, [FromQuery] string? warehouseId, CancellationToken cancellationToken)
    {
        using var _ = KartFlowContext.Push(FlowName);
        var result = await _sender.Send(new GetStockLevelQuery(sku, warehouseId), cancellationToken);
        return this.ToActionResult<StockLevelDto, StockLevelDto>(result, stockLevel => Ok(stockLevel));
    }

    /// <summary>Inventory & Stock Management flow's "Reorder Alert" dashboard view - every SKU/warehouse currently below its own ReplenishmentThreshold.</summary>
    [HttpGet("low-stock")]
    [ProducesResponseType(typeof(IReadOnlyList<StockLevelDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<StockLevelDto>>> GetLowStock([FromQuery] string? warehouseId, CancellationToken cancellationToken)
    {
        using var _ = KartFlowContext.Push(FlowName);
        var result = await _sender.Send(new GetLowStockQuery(warehouseId), cancellationToken);
        return this.ToActionResult<IReadOnlyList<StockLevelDto>, IReadOnlyList<StockLevelDto>>(result, list => Ok(list));
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
        using var _ = KartFlowContext.Push(FlowName);
        _logger.LogInformation("Stage {Stage}: replenish received for warehouse {WarehouseId}, sku {Sku}, qty {QtyAdded}.", "ReplenishStockRequestReceived", request.WarehouseId, request.Sku, request.QtyAdded);

        _logger.LogInformation("Stage {Stage}: dispatching ReplenishStockCommand for warehouse {WarehouseId}, sku {Sku}.", "ReplenishStockCommandDispatched", request.WarehouseId, request.Sku);
        var result = await _sender.Send(new ReplenishStockCommand(request.WarehouseId, request.Sku, request.QtyAdded), cancellationToken);
        return this.ToActionResult<StockLevelDto, StockLevelDto>(result, stockLevel => Ok(stockLevel));
    }

    /// <summary>
    /// Inventory & Stock Management flow: onboards a brand-new (WarehouseId, Sku) row - closes the
    /// previously-flagged gap where WarehouseStock.Provision was reachable only via a migration
    /// seed. AdminOnly-gated (master-data onboarding, not a routine replenishment).
    /// </summary>
    [HttpPost("provision")]
    [Authorize(Policy = AuthenticationExtensions.AdminOnlyPolicy)]
    [ProducesResponseType(typeof(StockLevelDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDto), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<StockLevelDto>> ProvisionWarehouseStock([FromBody] ProvisionWarehouseStockRequest request, CancellationToken cancellationToken)
    {
        using var _ = KartFlowContext.Push(FlowName);
        _logger.LogInformation("Stage {Stage}: provision received for warehouse {WarehouseId}, sku {Sku}.", "ProvisionWarehouseStockRequestReceived", request.WarehouseId, request.Sku);

        _logger.LogInformation("Stage {Stage}: dispatching ProvisionWarehouseStockCommand for warehouse {WarehouseId}, sku {Sku}.", "ProvisionWarehouseStockCommandDispatched", request.WarehouseId, request.Sku);
        var result = await _sender.Send(
            new ProvisionWarehouseStockCommand(request.WarehouseId, request.Sku, request.InitialQty, request.ReplenishmentThreshold, request.TargetStockingLevel),
            cancellationToken);
        return this.ToActionResult<StockLevelDto, StockLevelDto>(
            result,
            stockLevel => CreatedAtAction(nameof(GetStockLevel), new { sku = stockLevel.Sku, warehouseId = stockLevel.WarehouseId }, stockLevel));
    }

    /// <summary>Inventory & Stock Management flow's "Low Stock Threshold" stage - admin-adjustable, previously settable only at Provision time.</summary>
    [HttpPatch("{warehouseId}/{sku}/threshold")]
    [Authorize(Policy = AuthenticationExtensions.AdminOnlyPolicy)]
    [ProducesResponseType(typeof(StockLevelDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDto), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StockLevelDto>> UpdateReplenishmentThreshold(
        [FromRoute] string warehouseId,
        [FromRoute] string sku,
        [FromBody] UpdateReplenishmentThresholdRequest request,
        CancellationToken cancellationToken)
    {
        using var _ = KartFlowContext.Push(FlowName);
        _logger.LogInformation("Stage {Stage}: threshold update received for warehouse {WarehouseId}, sku {Sku}.", "UpdateReplenishmentThresholdRequestReceived", warehouseId, sku);

        _logger.LogInformation("Stage {Stage}: dispatching UpdateReplenishmentThresholdCommand for warehouse {WarehouseId}, sku {Sku}.", "UpdateReplenishmentThresholdCommandDispatched", warehouseId, sku);
        var result = await _sender.Send(
            new UpdateReplenishmentThresholdCommand(warehouseId, sku, request.ReplenishmentThreshold, request.TargetStockingLevel),
            cancellationToken);
        return this.ToActionResult<StockLevelDto, StockLevelDto>(result, stockLevel => Ok(stockLevel));
    }

    /// <summary>Inventory & Stock Management flow's "Stock Audit/Reconciliation" and "Update Qty" stages - overwrites AvailableQty to an admin-submitted physical count.</summary>
    [HttpPost("{warehouseId}/{sku}/reconcile")]
    [Authorize(Policy = AuthenticationExtensions.AdminOnlyPolicy)]
    [ProducesResponseType(typeof(StockReconciliationResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDto), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StockReconciliationResultDto>> ReconcileStock(
        [FromRoute] string warehouseId,
        [FromRoute] string sku,
        [FromBody] ReconcileStockRequest request,
        CancellationToken cancellationToken)
    {
        using var _ = KartFlowContext.Push(FlowName);
        _logger.LogInformation("Stage {Stage}: reconciliation received for warehouse {WarehouseId}, sku {Sku}, counted qty {CountedQty}.", "ReconcileStockRequestReceived", warehouseId, sku, request.CountedQty);

        _logger.LogInformation("Stage {Stage}: dispatching ReconcileStockCommand for warehouse {WarehouseId}, sku {Sku}.", "ReconcileStockCommandDispatched", warehouseId, sku);
        var result = await _sender.Send(new ReconcileStockCommand(warehouseId, sku, request.CountedQty, request.Reason), cancellationToken);
        return this.ToActionResult<StockReconciliationResultDto, StockReconciliationResultDto>(result, dto => Ok(dto));
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

/// <summary>provisionWarehouseStock requestBody shape.</summary>
public sealed record ProvisionWarehouseStockRequest(string WarehouseId, string Sku, int InitialQty, int ReplenishmentThreshold, int TargetStockingLevel);

/// <summary>updateReplenishmentThreshold requestBody shape.</summary>
public sealed record UpdateReplenishmentThresholdRequest(int ReplenishmentThreshold, int TargetStockingLevel);

/// <summary>reconcileStock requestBody shape.</summary>
public sealed record ReconcileStockRequest(int CountedQty, string Reason);
