using FluentAssertions;
using KartInventoryService.Application.Common.Interfaces;
using KartInventoryService.Application.Common.Services;
using KartInventoryService.Domain.Common;
using KartInventoryService.Domain.Inventory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KartInventoryService.UnitTests.Application;

public class ReservationReleaseServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly Mock<IReservationRepository> _reservationRepository = new();
    private readonly Mock<IWarehouseStockRepository> _stockRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private ReservationReleaseService CreateService() => new(
        _reservationRepository.Object,
        _stockRepository.Object,
        _unitOfWork.Object,
        TimeProvider.System,
        NullLogger<ReservationReleaseService>.Instance);

    private static Reservation CreateReservedReservation(string warehouseId, int qty) =>
        Reservation.Create(Guid.NewGuid(), "SKU-1", qty, new[] { (warehouseId, qty) }, TimeSpan.FromMinutes(15), "service:order", Now).Value;

    [Fact]
    public async Task ReleaseAsync_WhenReservationIsStillReserved_CreditsWarehouseStockBack_AndReturnsReleased()
    {
        var reservation = CreateReservedReservation("WH-1", 10);
        var stock = WarehouseStock.Provision("WH-1", "SKU-1", 40, 20, 100, "system:test", Now).Value;

        _reservationRepository.Setup(r => r.GetForUpdateAsync(reservation.ReservationId, It.IsAny<CancellationToken>())).ReturnsAsync(reservation);
        _stockRepository.Setup(r => r.GetForUpdateAsync("WH-1", "SKU-1", It.IsAny<CancellationToken>())).ReturnsAsync(stock);

        var service = CreateService();
        var result = await service.ReleaseAsync(reservation.ReservationId, ReservationReleaseReason.ExplicitCall, "service:order", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("released");
        stock.AvailableQty.Should().Be(50);
        _unitOfWork.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReleaseAsync_WhenReservationAlreadyReleased_DoesNotCreditStockAgain()
    {
        var reservation = CreateReservedReservation("WH-1", 10);
        reservation.Release(ReservationReleaseReason.ExplicitCall, "service:order", Now.AddMinutes(1));

        _reservationRepository.Setup(r => r.GetForUpdateAsync(reservation.ReservationId, It.IsAny<CancellationToken>())).ReturnsAsync(reservation);

        var service = CreateService();
        var result = await service.ReleaseAsync(reservation.ReservationId, ReservationReleaseReason.TtlExpiry, "system:inventory-ttl-sweep", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _stockRepository.Verify(r => r.GetForUpdateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReleaseAsync_WhenReservationDoesNotExist_RollsBackAndReturnsNotFound()
    {
        _reservationRepository
            .Setup(r => r.GetForUpdateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Reservation?)null);

        var service = CreateService();
        var result = await service.ReleaseAsync(Guid.NewGuid(), ReservationReleaseReason.ExplicitCall, "service:order", CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("not_found");
        _unitOfWork.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
