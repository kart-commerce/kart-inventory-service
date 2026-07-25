using KartInventoryService.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KartInventoryService.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps WarehouseStock to `warehouse_stock` exactly as database-design.md specifies - natural key
/// (warehouse_id, sku) IS the SELECT ... FOR UPDATE lock target, no surrogate key sits between
/// the lock and the natural identity.
/// </summary>
public sealed class WarehouseStockConfiguration : IEntityTypeConfiguration<WarehouseStock>
{
    public void Configure(EntityTypeBuilder<WarehouseStock> builder)
    {
        builder.ToTable("warehouse_stock", t =>
        {
            t.HasCheckConstraint("CK_warehouse_stock_available_qty_non_negative", "available_qty >= 0");
        });

        builder.HasKey(s => new { s.WarehouseId, s.Sku });

        builder.Property(s => s.WarehouseId).HasColumnName("warehouse_id").HasColumnType("text");
        builder.Property(s => s.Sku).HasColumnName("sku").HasColumnType("text");
        builder.Property(s => s.AvailableQty).HasColumnName("available_qty").IsRequired();
        builder.Property(s => s.ReplenishmentThreshold).HasColumnName("replenishment_threshold").IsRequired();
        builder.Property(s => s.TargetStockingLevel).HasColumnName("target_stocking_level").IsRequired();
        builder.Property(s => s.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(s => s.CreatedBy).HasColumnName("created_by").HasColumnType("text").IsRequired();
        builder.Property(s => s.UpdatedBy).HasColumnName("updated_by").HasColumnType("text").IsRequired();

        // Multi-warehouse candidate lookup before the fallback locks rows in ascending
        // warehouse_id order (requirement-spec.md Decision 3); also GET /inventory/{sku} with no
        // warehouse filter.
        builder.HasIndex(s => new { s.Sku, s.WarehouseId }).HasDatabaseName("idx_warehouse_stock_sku");

        builder.Ignore(s => s.DomainEvents);

        // Initial-load seed data (WarehouseStock.Provision's own doc comment: "a migration seed
        // or an equivalent trusted initial-load process" - tickets.md flags there being no admin
        // endpoint for onboarding warehouse/SKU master data). Two warehouses for the same SKU so
        // the multi-warehouse fallback (requirement-spec.md Decision 3) is exercisable locally.
        var seededAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        const string seedPrincipal = "system:initial-load";
        builder.HasData(
            new
            {
                WarehouseId = "WH-1",
                Sku = "DEMO-SKU-1",
                AvailableQty = 100,
                ReplenishmentThreshold = 20,
                TargetStockingLevel = 100,
                CreatedAt = seededAt,
                UpdatedAt = seededAt,
                CreatedBy = seedPrincipal,
                UpdatedBy = seedPrincipal,
            },
            new
            {
                WarehouseId = "WH-2",
                Sku = "DEMO-SKU-1",
                AvailableQty = 30,
                ReplenishmentThreshold = 10,
                TargetStockingLevel = 50,
                CreatedAt = seededAt,
                UpdatedAt = seededAt,
                CreatedBy = seedPrincipal,
                UpdatedBy = seedPrincipal,
            });
    }
}
