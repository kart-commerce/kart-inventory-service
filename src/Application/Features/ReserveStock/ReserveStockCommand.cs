using KartInventoryService.Application.Common.Models;
using KartInventoryService.Domain.Common;
using MediatR;

namespace KartInventoryService.Application.Features.ReserveStock;

/// <summary>
/// POST /inventory/reserve (api-contract.yaml reserveStock) - Order Service's saga step 1 (BRD
/// S12.1; ADR-0009). Single-warehouse-first, multi-warehouse fallback (requirement-spec.md
/// Decision 3).
/// </summary>
public sealed record ReserveStockCommand(Guid OrderId, string Sku, int Qty) : IRequest<Result<ReservationDto>>;
