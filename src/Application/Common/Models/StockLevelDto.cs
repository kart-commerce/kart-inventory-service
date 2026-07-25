namespace KartInventoryService.Application.Common.Models;

/// <summary>api-contract.yaml `StockLevel` schema exactly - GET /inventory/{sku} and the gRPC CheckAvailability response both project from this.</summary>
public sealed record StockLevelDto(string Sku, string? WarehouseId, int AvailableQty);
