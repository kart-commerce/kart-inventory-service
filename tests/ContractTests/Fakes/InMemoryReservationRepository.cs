using System.Collections.Concurrent;
using KartInventoryService.Application.Common.Interfaces;
using KartInventoryService.Domain.Inventory;

namespace KartInventoryService.ContractTests.Fakes;

public sealed class InMemoryReservationRepository : IReservationRepository
{
    private readonly ConcurrentDictionary<Guid, Reservation> _reservations = new();

    public void Seed(Reservation reservation) => _reservations[reservation.ReservationId] = reservation;

    public Task AddAsync(Reservation reservation, CancellationToken cancellationToken)
    {
        _reservations[reservation.ReservationId] = reservation;
        return Task.CompletedTask;
    }

    public Task<Reservation?> GetForUpdateAsync(Guid reservationId, CancellationToken cancellationToken) =>
        Task.FromResult(_reservations.GetValueOrDefault(reservationId));

    public Task<IReadOnlyList<Reservation>> GetReservedByOrderIdAsync(Guid orderId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Reservation>>(
            _reservations.Values.Where(r => r.OrderId == orderId && r.Status == ReservationStatus.Reserved).ToList());

    public Task<IReadOnlyList<Guid>> GetExpiredReservationIdsAsync(DateTimeOffset asOf, int batchSize, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Guid>>(_reservations.Values
            .Where(r => r.Status == ReservationStatus.Reserved && r.ExpiresAt < asOf)
            .OrderBy(r => r.ExpiresAt)
            .Take(batchSize)
            .Select(r => r.ReservationId)
            .ToList());
}
