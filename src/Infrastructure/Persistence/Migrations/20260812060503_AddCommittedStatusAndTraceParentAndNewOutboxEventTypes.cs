using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KartInventoryService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCommittedStatusAndTraceParentAndNewOutboxEventTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_reservations_order_id",
                table: "reservations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_reservations_status",
                table: "reservations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_inventory_outbox_events_event_type",
                table: "inventory_outbox_events");

            migrationBuilder.AddColumn<string>(
                name: "trace_parent",
                table: "inventory_outbox_events",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "idx_reservations_order_id",
                table: "reservations",
                column: "order_id",
                filter: "status IN ('reserved', 'committed')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_reservations_status",
                table: "reservations",
                sql: "status IN ('reserved', 'committed', 'released', 'expired')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_inventory_outbox_events_event_type",
                table: "inventory_outbox_events",
                sql: "event_type IN ('InventoryReserved', 'InventoryReservationFailed', 'InventoryReleased', 'InventoryReplenished', 'InventoryCommitted', 'LowStockDetected', 'InventoryReconciled', 'WarehouseStockProvisioned')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_reservations_order_id",
                table: "reservations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_reservations_status",
                table: "reservations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_inventory_outbox_events_event_type",
                table: "inventory_outbox_events");

            migrationBuilder.DropColumn(
                name: "trace_parent",
                table: "inventory_outbox_events");

            migrationBuilder.CreateIndex(
                name: "idx_reservations_order_id",
                table: "reservations",
                column: "order_id",
                filter: "status = 'reserved'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_reservations_status",
                table: "reservations",
                sql: "status IN ('reserved', 'released', 'expired')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_inventory_outbox_events_event_type",
                table: "inventory_outbox_events",
                sql: "event_type IN ('InventoryReserved', 'InventoryReservationFailed', 'InventoryReleased', 'InventoryReplenished')");
        }
    }
}
