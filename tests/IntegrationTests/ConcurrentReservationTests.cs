using FluentAssertions;
using KartInventoryService.Application.Common.Options;
using KartInventoryService.Application.Features.ReserveStock;
using KartInventoryService.Domain.Inventory;
using KartInventoryService.Infrastructure.Messaging;
using KartInventoryService.Infrastructure.Persistence;
using KartInventoryService.IntegrationTests.Fakes;
using KartInventoryService.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace KartInventoryService.IntegrationTests;

/// <summary>
/// The star test for "the platform's highest-contention service" (PLATFORM_BLUEPRINT.md):
/// fires many concurrent ReserveStock handlers, each on its own PostgreSQL connection/
/// transaction, against a SKU with limited stock, and proves the SELECT ... FOR UPDATE lock
/// actually serializes them - stock never goes negative and exactly as many reservations succeed
/// as there were units available, no more.
/// </summary>
[Collection("Postgres")]
public class ConcurrentReservationTests
{
    private const string Sku = "SKU-CONCURRENCY";
    private const string WarehouseId = "WH-CONCURRENCY";
    private const int InitialAvailableQty = 10;
    private const int ConcurrentAttempts = 25;

    private readonly PostgresContainerFixture _fixture;

    public ConcurrentReservationTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ConcurrentReserveStockCalls_NeverOversell_AndReserveExactlyAvailableQuantity()
    {
        await SeedWarehouseStockAsync();

        var results = await Task.WhenAll(Enumerable.Range(0, ConcurrentAttempts).Select(_ => AttemptReserveOnFreshConnectionAsync()));

        var successes = results.Count(r => r.IsSuccess);
        successes.Should().Be(InitialAvailableQty, "exactly as many units as were available should succeed, no more, no fewer");

        var failures = results.Where(r => r.IsFailure).ToList();
        failures.Should().OnlyContain(r => r.Error.Code == "insufficient_stock");

        await using var verificationContext = _fixture.CreateDbContext();
        var finalStock = await verificationContext.WarehouseStocks.AsNoTracking()
            .SingleAsync(s => s.WarehouseId == WarehouseId && s.Sku == Sku);
        finalStock.AvailableQty.Should().Be(0, "the oversell invariant - stock must never go negative under concurrent writers");

        var reservationCount = await verificationContext.Reservations.CountAsync(r => r.Sku == Sku);
        reservationCount.Should().Be(InitialAvailableQty);
    }

    private async Task SeedWarehouseStockAsync()
    {
        await using var dbContext = _fixture.CreateDbContext();
        var stock = WarehouseStock.Provision(WarehouseId, Sku, InitialAvailableQty, 2, InitialAvailableQty, "system:test-seed", DateTimeOffset.UtcNow).Value;
        dbContext.WarehouseStocks.Add(stock);
        await dbContext.SaveChangesAsync();
    }

    private async Task<Domain.Common.Result<Application.Common.Models.ReservationDto>> AttemptReserveOnFreshConnectionAsync()
    {
        await using var dbContext = _fixture.CreateDbContext();
        var stockRepository = new WarehouseStockRepository(dbContext);
        var reservationRepository = new ReservationRepository(dbContext);
        var unitOfWork = new EfUnitOfWork(dbContext);
        var outboxEventWriter = new OutboxEventWriter(dbContext, TimeProvider.System);
        var currentPrincipal = new FixedCurrentPrincipal("service:order");

        // Generous lock_timeout for this test - it is proving serialization correctness under
        // real contention, not the Resilience Budget's fail-fast behavior (a separate, narrower
        // concern already exercised by ReserveStockCommandHandlerTests' unit test).
        var options = Options.Create(new InventoryOptions { LockTimeoutMs = 5000 });

        var handler = new ReserveStockCommandHandler(
            stockRepository,
            reservationRepository,
            unitOfWork,
            outboxEventWriter,
            new NullStockCache(),
            currentPrincipal,
            TimeProvider.System,
            options,
            NullLogger<ReserveStockCommandHandler>.Instance);

        return await handler.Handle(new ReserveStockCommand(Guid.NewGuid(), Sku, 1), CancellationToken.None);
    }
}
