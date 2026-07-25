namespace KartInventoryService.Application.Common.Models;

/// <summary>api-contract.yaml Reservation.allocations entry shape.</summary>
public sealed record WarehouseAllocationDto(string WarehouseId, int Qty);
