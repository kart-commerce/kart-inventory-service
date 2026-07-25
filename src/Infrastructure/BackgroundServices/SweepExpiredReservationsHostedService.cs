using KartInventoryService.Application.Common.Interfaces;
using KartInventoryService.Application.Common.Options;
using KartInventoryService.Application.Common.Services;
using KartInventoryService.Domain.Inventory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KartInventoryService.Infrastructure.BackgroundServices;

/// <summary>
/// INV-5: requirement-spec.md Decision 2 - every SweepIntervalSeconds (default 60s), finds every
/// still-`reserved` hold whose expiry has passed (idx_reservations_expiry_sweep) and releases each
/// through the same idempotent ReservationReleaseService every other release trigger uses,
/// reason TtlExpiry. Deliberately blind to Payment/Order's live status - no heartbeat, no
/// query-back (design-decisions.md, "Reservation Hold Expiry Mechanism").
/// </summary>
public sealed class SweepExpiredReservationsHostedService : BackgroundService
{
    private const string SystemPrincipal = "system:inventory-ttl-sweep";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly InventoryOptions _options;
    private readonly ILogger<SweepExpiredReservationsHostedService> _logger;

    public SweepExpiredReservationsHostedService(
        IServiceScopeFactory scopeFactory,
        TimeProvider timeProvider,
        IOptions<InventoryOptions> options,
        ILogger<SweepExpiredReservationsHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(_options.SweepIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SweepOnceAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Reservation TTL sweep tick failed; will retry on the next tick.");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }

    private async Task SweepOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var reservationRepository = scope.ServiceProvider.GetRequiredService<IReservationRepository>();
        var releaseService = scope.ServiceProvider.GetRequiredService<ReservationReleaseService>();

        var expiredIds = await reservationRepository.GetExpiredReservationIdsAsync(
            _timeProvider.GetUtcNow(), _options.SweepBatchSize, cancellationToken);

        if (expiredIds.Count == 0)
        {
            return;
        }

        foreach (var reservationId in expiredIds)
        {
            await releaseService.ReleaseAsync(reservationId, ReservationReleaseReason.TtlExpiry, SystemPrincipal, cancellationToken);
        }

        _logger.LogInformation("TTL sweep released {Count} expired reservation(s).", expiredIds.Count);
    }
}
