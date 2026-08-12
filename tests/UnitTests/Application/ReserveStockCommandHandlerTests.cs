using FluentAssertions;
using KartInventoryService.Application.Common.Exceptions;
using KartInventoryService.Application.Common.Interfaces;
using KartInventoryService.Application.Common.Models;
using KartInventoryService.Application.Common.Options;
using KartInventoryService.Application.Features.ReserveStock;
using KartInventoryService.Domain.Inventory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KartInventoryService.UnitTests.Application;

public class ReserveStockCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly Mock<IWarehouseStockRepository> _stockRepository = new();
    private readonly Mock<IReservationRepository> _reservationRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IOutboxEventWriter> _outboxEventWriter = new();
    private readonly Mock<IStockCache> _stockCache = new();
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();

    private ReserveStockCommandHandler CreateHandler() => new(
        _stockRepository.Object,
        _reservationRepository.Object,
        _unitOfWork.Object,
        _outboxEventWriter.Object,
        _stockCache.Object,
        _currentPrincipal.Object,
        TimeProvider.System,
        Options.Create(new InventoryOptions()),
        NullLogger<ReserveStockCommandHandler>.Instance);

    private static WarehouseStock Stock(string warehouseId, int availableQty) =>
        WarehouseStock.Provision(warehouseId, "SKU-1", availableQty, 20, 100, "system:test", Now).Value;

    public ReserveStockCommandHandlerTests()
    {
        _currentPrincipal.Setup(p => p.ActingPrincipal).Returns("service:order");
    }

    [Fact]
    public async Task Handle_WhenSingleWarehouseCoversQty_DebitsThatWarehouseOnly_AndCommits()
    {
        var stock = Stock("WH-1", 50);
        _stockRepository
            .Setup(r => r.GetForUpdateBySkuAscendingAsync("SKU-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WarehouseStock> { stock });

        var handler = CreateHandler();
        var result = await handler.Handle(new ReserveStockCommand(Guid.NewGuid(), "SKU-1", 10), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Allocations.Should().ContainSingle().Which.WarehouseId.Should().Be("WH-1");
        stock.AvailableQty.Should().Be(40);
        _reservationRepository.Verify(r => r.AddAsync(It.IsAny<Reservation>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenNoSingleWarehouseSuffices_SplitsAcrossWarehouses()
    {
        var whA = Stock("WH-A", 6);
        var whB = Stock("WH-B", 6);
        _stockRepository
            .Setup(r => r.GetForUpdateBySkuAscendingAsync("SKU-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WarehouseStock> { whA, whB });

        var handler = CreateHandler();
        var result = await handler.Handle(new ReserveStockCommand(Guid.NewGuid(), "SKU-1", 10), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Allocations.Sum(a => a.Qty).Should().Be(10);
        (whA.AvailableQty + whB.AvailableQty).Should().Be(2);
    }

    [Fact]
    public async Task Handle_WhenInsufficientStock_RollsBackAndWritesFailureEvent_ReturnsInsufficientStockError()
    {
        var stock = Stock("WH-1", 3);
        _stockRepository
            .Setup(r => r.GetForUpdateBySkuAscendingAsync("SKU-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WarehouseStock> { stock });

        var handler = CreateHandler();
        var result = await handler.Handle(new ReserveStockCommand(Guid.NewGuid(), "SKU-1", 10), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("insufficient_stock");
        stock.AvailableQty.Should().Be(3);
        _unitOfWork.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _outboxEventWriter.Verify(
            w => w.EnqueueAsync("InventoryReservationFailed", It.IsAny<string>(), It.IsAny<object>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenNoStockProvisionedForSku_RollsBackAndReturnsNotFound()
    {
        _stockRepository
            .Setup(r => r.GetForUpdateBySkuAscendingAsync("UNKNOWN-SKU", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WarehouseStock>());

        var handler = CreateHandler();
        var result = await handler.Handle(new ReserveStockCommand(Guid.NewGuid(), "UNKNOWN-SKU", 1), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("not_found");
    }

    [Fact]
    public async Task Handle_WhenLockAcquisitionTimesOut_RollsBackAndReturnsLockTimeoutError()
    {
        _stockRepository
            .Setup(r => r.GetForUpdateBySkuAscendingAsync("SKU-1", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new LockAcquisitionTimeoutException("timed out", new Exception()));

        var handler = CreateHandler();
        var result = await handler.Handle(new ReserveStockCommand(Guid.NewGuid(), "SKU-1", 1), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("lock_timeout");
        _unitOfWork.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
