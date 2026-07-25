using KartInventoryService.Application.Common.Models;
using KartInventoryService.Domain.Common;
using MediatR;

namespace KartInventoryService.Application.Features.ReleaseReservation;

/// <summary>POST /inventory/release (api-contract.yaml releaseReservation) - explicit, idempotent release.</summary>
public sealed record ReleaseReservationCommand(Guid ReservationId) : IRequest<Result<ReservationDto>>;
