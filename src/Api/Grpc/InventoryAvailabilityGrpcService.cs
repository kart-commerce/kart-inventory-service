using global::Grpc.Core;
using KartInventoryService.Application.Features.GetStockLevel;
using MediatR;

namespace KartInventoryService.Api.Grpc;

/// <summary>
/// INV-7: implements the proto-generated InventoryAvailabilityService (Protos/inventory_availability.proto),
/// reusing INV-6's GetStockLevelQuery/cache-aside read path directly rather than a second read
/// implementation (tickets.md: "INV-7 ... reuses INV-6's same cache-aside read path under a
/// different protocol"). A SKU with no provisioned stock degrades to availableQty 0/sufficient
/// false rather than an RPC fault - Cart's own client already treats this call as best-effort
/// and fails open on error (architecture.md's "API Surface Consistency Note").
/// </summary>
public sealed class InventoryAvailabilityGrpcService : InventoryAvailabilityService.InventoryAvailabilityServiceBase
{
    private readonly ISender _sender;

    public InventoryAvailabilityGrpcService(ISender sender)
    {
        _sender = sender;
    }

    public override async Task<CheckAvailabilityResponse> CheckAvailability(CheckAvailabilityRequest request, ServerCallContext context)
    {
        var result = await _sender.Send(new GetStockLevelQuery(request.Sku, WarehouseId: null), context.CancellationToken);
        var availableQty = result.IsSuccess ? result.Value.AvailableQty : 0;

        return new CheckAvailabilityResponse
        {
            Sku = request.Sku,
            AvailableQty = availableQty,
            Sufficient = availableQty >= request.RequestedQty,
        };
    }
}
