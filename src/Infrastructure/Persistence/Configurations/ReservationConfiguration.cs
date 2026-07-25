using KartInventoryService.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KartInventoryService.Infrastructure.Persistence.Configurations;

/// <summary>Maps Reservation to `reservations` exactly as database-design.md specifies, plus its owned Allocations collection (reservation_allocations).</summary>
public sealed class ReservationConfiguration : IEntityTypeConfiguration<Reservation>
{
    public void Configure(EntityTypeBuilder<Reservation> builder)
    {
        builder.ToTable("reservations", t =>
        {
            t.HasCheckConstraint("CK_reservations_qty_positive", "qty > 0");
            t.HasCheckConstraint("CK_reservations_status", "status IN ('reserved', 'released', 'expired')");
            t.HasCheckConstraint(
                "CK_reservations_release_reason",
                "release_reason IS NULL OR release_reason IN ('explicit_call', 'order_cancelled', 'compensation_triggered', 'ttl_expiry')");
        });

        builder.HasKey(r => r.ReservationId);
        builder.Property(r => r.ReservationId).HasColumnName("reservation_id").ValueGeneratedNever();

        builder.Property(r => r.OrderId).HasColumnName("order_id").IsRequired();
        builder.Property(r => r.Sku).HasColumnName("sku").HasColumnType("text").IsRequired();
        builder.Property(r => r.Qty).HasColumnName("qty").IsRequired();

        builder.Property(r => r.Status)
            .HasColumnName("status")
            .HasColumnType("text")
            .HasConversion(status => ToStatusColumn(status), value => ParseStatus(value))
            .IsRequired();

        builder.Property(r => r.ReleaseReason)
            .HasColumnName("release_reason")
            .HasColumnType("text")
            .HasConversion(reason => ToReleaseReasonColumn(reason), value => ParseReleaseReason(value));

        builder.Property(r => r.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(r => r.ExpiresAt).HasColumnName("expires_at").IsRequired();
        builder.Property(r => r.ReleasedAt).HasColumnName("released_at");
        builder.Property(r => r.CreatedBy).HasColumnName("created_by").HasColumnType("text").IsRequired();
        builder.Property(r => r.UpdatedBy).HasColumnName("updated_by").HasColumnType("text").IsRequired();

        // OrderCancelled/OrderCompensationTriggered consumers' "find the live reservation(s) for
        // this orderId" lookup - partial index, shrinks as reservations terminate.
        builder.HasIndex(r => r.OrderId).HasDatabaseName("idx_reservations_order_id").HasFilter("status = 'reserved'");

        // The 60-second TTL sweep's "find every still-reserved hold whose expiry has passed" scan.
        builder.HasIndex(r => r.ExpiresAt).HasDatabaseName("idx_reservations_expiry_sweep").HasFilter("status = 'reserved'");

        builder.HasMany(r => r.Allocations)
            .WithOne()
            .HasForeignKey(a => a.ReservationId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(r => r.Allocations).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(r => r.DomainEvents);
    }

    private static string ToStatusColumn(ReservationStatus status) => status switch
    {
        ReservationStatus.Reserved => "reserved",
        ReservationStatus.Released => "released",
        ReservationStatus.Expired => "expired",
        _ => throw new InvalidOperationException($"Unknown reservation status '{status}'."),
    };

    private static ReservationStatus ParseStatus(string value) => value switch
    {
        "reserved" => ReservationStatus.Reserved,
        "released" => ReservationStatus.Released,
        "expired" => ReservationStatus.Expired,
        _ => throw new InvalidOperationException($"Unknown reservation status column value '{value}'."),
    };

    private static string? ToReleaseReasonColumn(ReservationReleaseReason? reason) => reason switch
    {
        null => null,
        ReservationReleaseReason.ExplicitCall => "explicit_call",
        ReservationReleaseReason.OrderCancelled => "order_cancelled",
        ReservationReleaseReason.CompensationTriggered => "compensation_triggered",
        ReservationReleaseReason.TtlExpiry => "ttl_expiry",
        _ => throw new InvalidOperationException($"Unknown release reason '{reason}'."),
    };

    private static ReservationReleaseReason? ParseReleaseReason(string? value) => value switch
    {
        null => null,
        "explicit_call" => ReservationReleaseReason.ExplicitCall,
        "order_cancelled" => ReservationReleaseReason.OrderCancelled,
        "compensation_triggered" => ReservationReleaseReason.CompensationTriggered,
        "ttl_expiry" => ReservationReleaseReason.TtlExpiry,
        _ => throw new InvalidOperationException($"Unknown release reason column value '{value}'."),
    };
}
