using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace KartInventoryService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "inventory_outbox_events",
                columns: table => new
                {
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "text", nullable: false),
                    aggregate_ref = table.Column<string>(type: "text", nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_by = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_outbox_events", x => x.event_id);
                    table.CheckConstraint("CK_inventory_outbox_events_event_type", "event_type IN ('InventoryReserved', 'InventoryReservationFailed', 'InventoryReleased', 'InventoryReplenished')");
                });

            migrationBuilder.CreateTable(
                name: "reservations",
                columns: table => new
                {
                    reservation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sku = table.Column<string>(type: "text", nullable: false),
                    qty = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    release_reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    released_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_by = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reservations", x => x.reservation_id);
                    table.CheckConstraint("CK_reservations_qty_positive", "qty > 0");
                    table.CheckConstraint("CK_reservations_release_reason", "release_reason IS NULL OR release_reason IN ('explicit_call', 'order_cancelled', 'compensation_triggered', 'ttl_expiry')");
                    table.CheckConstraint("CK_reservations_status", "status IN ('reserved', 'released', 'expired')");
                });

            migrationBuilder.CreateTable(
                name: "warehouse_stock",
                columns: table => new
                {
                    warehouse_id = table.Column<string>(type: "text", nullable: false),
                    sku = table.Column<string>(type: "text", nullable: false),
                    available_qty = table.Column<int>(type: "integer", nullable: false),
                    replenishment_threshold = table.Column<int>(type: "integer", nullable: false),
                    target_stocking_level = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_by = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_warehouse_stock", x => new { x.warehouse_id, x.sku });
                    table.CheckConstraint("CK_warehouse_stock_available_qty_non_negative", "available_qty >= 0");
                });

            migrationBuilder.CreateTable(
                name: "reservation_allocations",
                columns: table => new
                {
                    reservation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<string>(type: "text", nullable: false),
                    qty = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_by = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reservation_allocations", x => new { x.reservation_id, x.warehouse_id });
                    table.CheckConstraint("CK_reservation_allocations_qty_positive", "qty > 0");
                    table.ForeignKey(
                        name: "FK_reservation_allocations_reservations_reservation_id",
                        column: x => x.reservation_id,
                        principalTable: "reservations",
                        principalColumn: "reservation_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "warehouse_stock",
                columns: new[] { "sku", "warehouse_id", "available_qty", "created_at", "created_by", "replenishment_threshold", "target_stocking_level", "updated_at", "updated_by" },
                values: new object[,]
                {
                    { "DEMO-SKU-1", "WH-1", 100, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system:initial-load", 20, 100, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system:initial-load" },
                    { "DEMO-SKU-1", "WH-2", 30, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system:initial-load", 10, 50, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system:initial-load" }
                });

            migrationBuilder.CreateIndex(
                name: "idx_inventory_outbox_unpublished",
                table: "inventory_outbox_events",
                column: "occurred_at",
                filter: "published_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "idx_reservation_allocations_reservation",
                table: "reservation_allocations",
                column: "reservation_id");

            migrationBuilder.CreateIndex(
                name: "idx_reservations_expiry_sweep",
                table: "reservations",
                column: "expires_at",
                filter: "status = 'reserved'");

            migrationBuilder.CreateIndex(
                name: "idx_reservations_order_id",
                table: "reservations",
                column: "order_id",
                filter: "status = 'reserved'");

            migrationBuilder.CreateIndex(
                name: "idx_warehouse_stock_sku",
                table: "warehouse_stock",
                columns: new[] { "sku", "warehouse_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inventory_outbox_events");

            migrationBuilder.DropTable(
                name: "reservation_allocations");

            migrationBuilder.DropTable(
                name: "warehouse_stock");

            migrationBuilder.DropTable(
                name: "reservations");
        }
    }
}
