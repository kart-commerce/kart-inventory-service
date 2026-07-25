namespace KartInventoryService.Domain.Inventory;

/// <summary>
/// Result of Reservation.Release - distinguishes a genuine state transition (which must credit
/// WarehouseStock back and publish InventoryReleased) from an idempotent no-op against an
/// already-terminal reservation (which must do neither, or a redelivered/duplicate release
/// trigger would double-credit stock or double-publish - requirement-spec.md S4).
/// </summary>
public enum ReleaseOutcome
{
    Released,
    AlreadyTerminal
}
