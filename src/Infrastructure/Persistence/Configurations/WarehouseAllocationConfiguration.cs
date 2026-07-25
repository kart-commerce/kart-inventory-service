using KartInventoryService.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KartInventoryService.Infrastructure.Persistence.Configurations;

/// <summary>Maps WarehouseAllocation to `reservation_allocations` exactly as database-design.md specifies. The FK relationship itself is configured from Reservation's side (ReservationConfiguration.HasMany).</summary>
public sealed class WarehouseAllocationConfiguration : IEntityTypeConfiguration<WarehouseAllocation>
{
    public void Configure(EntityTypeBuilder<WarehouseAllocation> builder)
    {
        builder.ToTable("reservation_allocations", t =>
        {
            t.HasCheckConstraint("CK_reservation_allocations_qty_positive", "qty > 0");
        });

        builder.HasKey(a => new { a.ReservationId, a.WarehouseId });

        builder.Property(a => a.ReservationId).HasColumnName("reservation_id");
        builder.Property(a => a.WarehouseId).HasColumnName("warehouse_id").HasColumnType("text");
        builder.Property(a => a.Qty).HasColumnName("qty").IsRequired();
        builder.Property(a => a.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(a => a.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(a => a.CreatedBy).HasColumnName("created_by").HasColumnType("text").IsRequired();
        builder.Property(a => a.UpdatedBy).HasColumnName("updated_by").HasColumnType("text").IsRequired();

        // Release's own read: "which warehouse_stock rows does this reservation's credit-back
        // need to touch" - direct indexed lookup by the PK's leading column.
        builder.HasIndex(a => a.ReservationId).HasDatabaseName("idx_reservation_allocations_reservation");
    }
}
