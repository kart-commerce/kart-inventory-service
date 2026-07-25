using KartInventoryService.Domain.Inventory;

namespace KartInventoryService.Application.Common.Models;

/// <summary>api-contract.yaml `Reservation` schema exactly - the shared read shape returned by ReserveStock/ReleaseReservation.</summary>
public sealed record ReservationDto(
    Guid ReservationId,
    Guid OrderId,
    string Sku,
    int Qty,
    string Status,
    IReadOnlyList<WarehouseAllocationDto> Allocations,
    DateTimeOffset ExpiresAt)
{
    public static ReservationDto FromDomain(Reservation reservation) => new(
        reservation.ReservationId,
        reservation.OrderId,
        reservation.Sku,
        reservation.Qty,
        reservation.Status.ToString().ToLowerInvariant(),
        reservation.Allocations.Select(a => new WarehouseAllocationDto(a.WarehouseId, a.Qty)).ToList(),
        reservation.ExpiresAt);
}
