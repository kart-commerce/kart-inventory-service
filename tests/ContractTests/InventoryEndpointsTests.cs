using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using KartInventoryService.Domain.Inventory;
using Xunit;

namespace KartInventoryService.ContractTests;

/// <summary>
/// HTTP wire-shape assertions (status codes, JSON field names, RBAC gating) against
/// contracts/api-contract.yaml - not domain-logic tests (those live in UnitTests/IntegrationTests).
/// </summary>
public class InventoryEndpointsTests : IClassFixture<InventoryApiFactory>
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
    private readonly InventoryApiFactory _factory;

    public InventoryEndpointsTests(InventoryApiFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClient(params string[] roles)
    {
        var client = _factory.CreateClient();
        if (roles.Length > 0)
        {
            client.DefaultRequestHeaders.Add("X-Test-Roles", string.Join(",", roles));
        }

        return client;
    }

    [Fact]
    public async Task GetStockLevel_WhenSkuExists_Returns200WithContractShape()
    {
        _factory.StockRepository.Seed(WarehouseStock.Provision("WH-1", "SKU-GET-OK", 42, 5, 100, "system:test", Now).Value);
        var client = CreateClient();

        var response = await client.GetAsync("/v1/inventory/SKU-GET-OK");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("\"sku\"").And.Contain("\"availableQty\":42");
    }

    [Fact]
    public async Task GetStockLevel_WhenSkuUnknown_Returns404WithProblemShape()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/v1/inventory/SKU-DOES-NOT-EXIST");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("\"code\":\"not_found\"").And.Contain("\"message\"");
    }

    [Fact]
    public async Task ReserveStock_WithoutOrderServiceRole_Returns403()
    {
        var client = CreateClient(); // no X-Test-Roles header at all

        var response = await client.PostAsJsonAsync("/v1/inventory/reserve", new { orderId = Guid.NewGuid(), sku = "SKU-ANY", qty = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ReserveStock_WithSufficientStock_Returns201WithContractShape()
    {
        _factory.StockRepository.Seed(WarehouseStock.Provision("WH-1", "SKU-RESERVE-OK", 10, 2, 100, "system:test", Now).Value);
        var client = CreateClient("service:order");
        var orderId = Guid.NewGuid();

        var response = await client.PostAsJsonAsync("/v1/inventory/reserve", new { orderId, sku = "SKU-RESERVE-OK", qty = 3 });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("\"reservationId\"").And.Contain("\"status\":\"reserved\"").And.Contain("\"allocations\"");
    }

    [Fact]
    public async Task ReserveStock_WithInsufficientStock_Returns409WithProblemShape()
    {
        _factory.StockRepository.Seed(WarehouseStock.Provision("WH-1", "SKU-RESERVE-INSUFFICIENT", 1, 1, 100, "system:test", Now).Value);
        var client = CreateClient("service:order");

        var response = await client.PostAsJsonAsync("/v1/inventory/reserve", new { orderId = Guid.NewGuid(), sku = "SKU-RESERVE-INSUFFICIENT", qty = 5 });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("\"code\":\"insufficient_stock\"");
    }

    [Fact]
    public async Task ReleaseReservation_WhenReservationExists_Returns200_AndIsIdempotentOnSecondCall()
    {
        var reservation = Reservation.Create(Guid.NewGuid(), "SKU-RELEASE", 2, new[] { ("WH-1", 2) }, TimeSpan.FromMinutes(15), "service:order", Now).Value;
        _factory.ReservationRepository.Seed(reservation);
        var client = CreateClient("service:order");

        var firstResponse = await client.PostAsJsonAsync("/v1/inventory/release", new { reservationId = reservation.ReservationId });
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await firstResponse.Content.ReadAsStringAsync()).Should().Contain("\"status\":\"released\"");

        var secondResponse = await client.PostAsJsonAsync("/v1/inventory/release", new { reservationId = reservation.ReservationId });
        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK, "release is idempotent - a second call against an already-terminal reservation is a no-op, not an error");
    }

    [Fact]
    public async Task ReleaseReservation_WhenUnknown_Returns404()
    {
        var client = CreateClient("service:order");

        var response = await client.PostAsJsonAsync("/v1/inventory/release", new { reservationId = Guid.NewGuid() });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ReplenishStock_WithAdminRole_Returns200()
    {
        _factory.StockRepository.Seed(WarehouseStock.Provision("WH-1", "SKU-REPLENISH", 5, 10, 100, "system:test", Now).Value);
        var client = CreateClient("admin");

        var response = await client.PostAsJsonAsync("/v1/inventory/replenish", new { warehouseId = "WH-1", sku = "SKU-REPLENISH", qtyAdded = 20 });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("\"availableQty\":25");
    }

    [Fact]
    public async Task ReplenishStock_WithoutAuthorizedRole_Returns403()
    {
        var client = CreateClient("service:order"); // a role that exists but isn't admin/replenishment-trigger

        var response = await client.PostAsJsonAsync("/v1/inventory/replenish", new { warehouseId = "WH-1", sku = "SKU-REPLENISH", qtyAdded = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
