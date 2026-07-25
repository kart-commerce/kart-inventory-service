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
/// design-decisions.md "Event Publish Atomicity" - every published event is inserted into
/// inventory_outbox_events in the same transaction/SaveChanges call as the write it describes.
/// Mirrors kart-category-service's CategoryOutboxTests.
/// </summary>
[Collection("Postgres")]
public class OutboxAtomicityTests
{
    private readonly PostgresContainerFixture _fixture;

    public OutboxAtomicityTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SuccessfulReserve_WritesReservationAndInventoryReservedOutboxRow_Together()
    {
        const string sku = "SKU-OUTBOX-SUCCESS";
        const string warehouseId = "WH-OUTBOX";

        await using (var seedContext = _fixture.CreateDbContext())
        {
            var stock = WarehouseStock.Provision(warehouseId, sku, 5, 1, 5, "system:test-seed", DateTimeOffset.UtcNow).Value;
            seedContext.WarehouseStocks.Add(stock);
            await seedContext.SaveChangesAsync();
        }

        var orderId = Guid.NewGuid();
        await using (var dbContext = _fixture.CreateDbContext())
        {
            var handler = new ReserveStockCommandHandler(
                new WarehouseStockRepository(dbContext),
                new ReservationRepository(dbContext),
                new EfUnitOfWork(dbContext),
                new OutboxEventWriter(dbContext, TimeProvider.System),
                new FixedCurrentPrincipal("service:order"),
                TimeProvider.System,
                Options.Create(new InventoryOptions()),
                NullLogger<ReserveStockCommandHandler>.Instance);

            var result = await handler.Handle(new ReserveStockCommand(orderId, sku, 2), CancellationToken.None);
            result.IsSuccess.Should().BeTrue();
        }

        await using var verificationContext = _fixture.CreateDbContext();
        var reservation = await verificationContext.Reservations.AsNoTracking().SingleAsync(r => r.OrderId == orderId);
        var outboxRow = await verificationContext.OutboxEvents.AsNoTracking()
            .SingleAsync(e => e.EventType == "InventoryReserved" && e.AggregateRef == reservation.ReservationId.ToString());

        outboxRow.Payload.Should().Contain(orderId.ToString()).And.Contain(sku);
        outboxRow.PublishedAt.Should().BeNull("the relay hasn't run yet - this row is only durably queued, not yet published");
    }

    [Fact]
    public async Task FailedReserve_WritesInventoryReservationFailedOutboxRow_WithNoReservationCreated()
    {
        const string sku = "SKU-OUTBOX-FAILURE";
        const string warehouseId = "WH-OUTBOX-2";

        await using (var seedContext = _fixture.CreateDbContext())
        {
            var stock = WarehouseStock.Provision(warehouseId, sku, 1, 1, 5, "system:test-seed", DateTimeOffset.UtcNow).Value;
            seedContext.WarehouseStocks.Add(stock);
            await seedContext.SaveChangesAsync();
        }

        var orderId = Guid.NewGuid();
        await using (var dbContext = _fixture.CreateDbContext())
        {
            var handler = new ReserveStockCommandHandler(
                new WarehouseStockRepository(dbContext),
                new ReservationRepository(dbContext),
                new EfUnitOfWork(dbContext),
                new OutboxEventWriter(dbContext, TimeProvider.System),
                new FixedCurrentPrincipal("service:order"),
                TimeProvider.System,
                Options.Create(new InventoryOptions()),
                NullLogger<ReserveStockCommandHandler>.Instance);

            var result = await handler.Handle(new ReserveStockCommand(orderId, sku, 10), CancellationToken.None);
            result.IsFailure.Should().BeTrue();
        }

        await using var verificationContext = _fixture.CreateDbContext();
        (await verificationContext.Reservations.AnyAsync(r => r.OrderId == orderId)).Should().BeFalse();

        var outboxRow = await verificationContext.OutboxEvents.AsNoTracking()
            .SingleAsync(e => e.EventType == "InventoryReservationFailed" && e.AggregateRef == $"{orderId}:{sku}");
        outboxRow.Payload.Should().Contain(orderId.ToString()).And.Contain(sku);
    }
}
