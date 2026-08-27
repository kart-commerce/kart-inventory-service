using KartInventoryService.Application.Common.Interfaces;
using KartInventoryService.Domain.Inventory;
using Microsoft.EntityFrameworkCore;

namespace KartInventoryService.Infrastructure.Persistence;

public sealed class ReservationRepository : IReservationRepository
{
    private readonly InventoryDbContext _dbContext;

    public ReservationRepository(InventoryDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(Reservation reservation, CancellationToken cancellationToken) =>
        await _dbContext.Reservations.AddAsync(reservation, cancellationToken);

    public async Task<Reservation?> GetForUpdateAsync(Guid reservationId, CancellationToken cancellationToken)
    {
        var reservation = await _dbContext.Reservations
            .FromSqlInterpolated($"SELECT * FROM reservations WHERE reservation_id = {reservationId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

        if (reservation is not null)
        {
            // Explicit load - the raw-SQL root query above can't compose an Include() around a
            // FOR UPDATE lock, so the Allocations navigation is loaded separately here instead.
            await _dbContext.Entry(reservation).Collection(r => r.Allocations).LoadAsync(cancellationToken);
        }

        return reservation;
    }

    public async Task<IReadOnlyList<Reservation>> GetReservedByOrderIdAsync(Guid orderId, CancellationToken cancellationToken) =>
        await _dbContext.Reservations
            .AsNoTracking()
            .Where(r => r.OrderId == orderId && (r.Status == ReservationStatus.Reserved || r.Status == ReservationStatus.Committed))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Guid>> GetExpiredReservationIdsAsync(DateTimeOffset asOf, int batchSize, CancellationToken cancellationToken) =>
        await _dbContext.Reservations
            .AsNoTracking()
            .Where(r => r.Status == ReservationStatus.Reserved && r.ExpiresAt < asOf)
            .OrderBy(r => r.ExpiresAt)
            .Take(batchSize)
            .Select(r => r.ReservationId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Reservation>> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken) =>
        await _dbContext.Reservations
            .AsNoTracking()
            .Include(r => r.Allocations)
            .Where(r => r.OrderId == orderId)
            .ToListAsync(cancellationToken);
}
