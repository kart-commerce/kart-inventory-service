using KartInventoryService.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KartInventoryService.Infrastructure.Persistence.Configurations;

/// <summary>Maps InventoryOutboxEvent to `inventory_outbox_events` exactly as database-design.md specifies.</summary>
public sealed class InventoryOutboxEventConfiguration : IEntityTypeConfiguration<InventoryOutboxEvent>
{
    public void Configure(EntityTypeBuilder<InventoryOutboxEvent> builder)
    {
        builder.ToTable("inventory_outbox_events", t =>
        {
            t.HasCheckConstraint(
                "CK_inventory_outbox_events_event_type",
                "event_type IN ('InventoryReserved', 'InventoryReservationFailed', 'InventoryReleased', 'InventoryReplenished', 'InventoryCommitted', 'LowStockDetected', 'InventoryReconciled', 'WarehouseStockProvisioned')");
        });

        builder.HasKey(e => e.EventId);
        builder.Property(e => e.EventId).HasColumnName("event_id").ValueGeneratedNever();

        builder.Property(e => e.EventType).HasColumnName("event_type").HasColumnType("text").IsRequired();
        builder.Property(e => e.AggregateRef).HasColumnName("aggregate_ref").HasColumnType("text").IsRequired();
        builder.Property(e => e.Payload).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
        builder.Property(e => e.OccurredAt).HasColumnName("occurred_at").IsRequired();
        builder.Property(e => e.PublishedAt).HasColumnName("published_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by").HasColumnType("text").IsRequired();
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by").HasColumnType("text").IsRequired();

        // Kart flow-instrumentation standard: every outbox table needs this column so
        // OutboxRelayHostedService (a background poller, unrelated async context) can continue
        // the originating request's trace instead of starting a disconnected new one.
        builder.Property(e => e.TraceParent).HasColumnName("trace_parent").HasColumnType("text");

        // Standard Outbox poller scan (BRD S11) - an index range-scan, not a full-table scan.
        builder.HasIndex(e => e.OccurredAt).HasDatabaseName("idx_inventory_outbox_unpublished").HasFilter("published_at IS NULL");
    }
}
