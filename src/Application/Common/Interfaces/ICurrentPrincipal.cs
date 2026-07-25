namespace KartInventoryService.Application.Common.Interfaces;

/// <summary>
/// Resolves the acting principal for BRD S24.3 audit stamping (created_by/updated_by) for
/// HTTP-triggered writes (reserve/release/replenish - always Order Service's or Admin's own
/// client-credentials principal, requirement-spec.md S24.1.2/S24.3). Background processes (the
/// TTL sweep, the OrderCancelled/OrderCompensationTriggered consumers) are not HTTP requests and
/// pass their own well-known `system:*` principal directly instead of using this.
/// </summary>
public interface ICurrentPrincipal
{
    string ActingPrincipal { get; }
}
