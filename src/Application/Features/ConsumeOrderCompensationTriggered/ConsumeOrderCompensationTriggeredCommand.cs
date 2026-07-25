using MediatR;

namespace KartInventoryService.Application.Features.ConsumeOrderCompensationTriggered;

/// <summary>
/// Dispatched on receipt of Order's OrderCompensationTriggered event (BRD S12.2's saga
/// compensation trigger; ADR-0007) - releases every still-`reserved` reservation for this
/// orderId. `Reason` is carried for audit/logging only (event-contract.md payload orderId,
/// reason) - it does not change the release logic.
/// </summary>
public sealed record ConsumeOrderCompensationTriggeredCommand(Guid OrderId, string Reason) : IRequest;
