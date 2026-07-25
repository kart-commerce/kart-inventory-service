namespace KartInventoryService.Domain.Inventory;

/// <summary>
/// ddd-model.md: child entity of Reservation, no lifecycle or identity independent of its parent -
/// one row per warehouse a Reservation drew from. A single-warehouse reservation has exactly one;
/// the multi-warehouse fallback (requirement-spec.md Decision 3) has two or more, all created
/// atomically in the same transaction as the parent Reservation.
/// </summary>
public sealed class WarehouseAllocation
{
    public Guid ReservationId { get; private set; }
    public string WarehouseId { get; private set; } = string.Empty;
    public int Qty { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;
    public string UpdatedBy { get; private set; } = string.Empty;

    // EF Core materialization only.
    private WarehouseAllocation()
    {
    }

    internal WarehouseAllocation(Guid reservationId, string warehouseId, int qty, string actingPrincipal, DateTimeOffset now)
    {
        ReservationId = reservationId;
        WarehouseId = warehouseId;
        Qty = qty;
        CreatedAt = now;
        UpdatedAt = now;
        CreatedBy = actingPrincipal;
        UpdatedBy = actingPrincipal;
    }
}
