using KartInventoryService.Application.Common.Models;
using KartInventoryService.Domain.Common;
using MediatR;

namespace KartInventoryService.Application.Features.GetOrderAllocations;

/// <summary>
/// Order Management (Admin) flow #7's "Assign Warehouse" view — GET /v1/inventory/orders/{orderId}/allocations.
/// Warehouse allocation itself stays fully automatic inside the reserve saga (requirement-spec.md
/// Decision 3's single-warehouse-first/multi-warehouse-fallback logic) — this is read-only
/// visibility for admins into a decision the system already made, not a new admin override.
/// </summary>
public sealed record GetOrderAllocationsQuery(Guid OrderId) : IRequest<Result<IReadOnlyList<ReservationDto>>>;
