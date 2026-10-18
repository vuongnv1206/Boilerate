using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Boilerate.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class PhysicalShipping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PhysicalStockReservation_State",
                schema: "Ordering",
                table: "PhysicalStockReservations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PaymentReceipt_Source",
                schema: "Payments",
                table: "PaymentReceipts");

            migrationBuilder.EnsureSchema(
                name: "Shipping");

            migrationBuilder.AddColumn<int>(
                name: "ConsumedQuantity",
                schema: "Ordering",
                table: "PhysicalStockReservations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "PhysicalDispatchStarted",
                schema: "Ordering",
                table: "Orders",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "Shipments",
                schema: "Shipping",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    Carrier = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TrackingNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DispatchedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeliveredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeliveryOperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LastModifiedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Version = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shipments", x => x.Id);
                    table.UniqueConstraint("AK_Shipments_Id_OrderId", x => new { x.Id, x.OrderId });
                    table.CheckConstraint("CK_Shipment_Delivery", "(\"Status\" = 1 AND \"DeliveredAtUtc\" IS NULL AND \"DeliveryOperationId\" IS NULL) OR (\"Status\" = 2 AND \"DeliveredAtUtc\" IS NOT NULL AND \"DeliveredAtUtc\" >= \"DispatchedAtUtc\" AND \"DeliveryOperationId\" IS NOT NULL)");
                    table.CheckConstraint("CK_Shipment_Status", "\"Status\" IN (1,2)");
                    table.ForeignKey(
                        name: "FK_Shipments_Orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "Ordering",
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ShipmentItems",
                schema: "Shipping",
                columns: table => new
                {
                    ShipmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VariantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StockMovementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FulfillmentKind = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShipmentItems", x => new { x.ShipmentId, x.OrderItemId });
                    table.CheckConstraint("CK_ShipmentItem_FulfillmentKind", "\"FulfillmentKind\" = 1");
                    table.CheckConstraint("CK_ShipmentItem_Quantity", "\"Quantity\" BETWEEN 1 AND 999");
                    table.ForeignKey(
                        name: "FK_ShipmentItems_OrderItems_OrderItemId_OrderId_VariantId_FulfillmentKind",
                        columns: x => new { x.OrderItemId, x.OrderId, x.VariantId, x.FulfillmentKind },
                        principalSchema: "Ordering",
                        principalTable: "OrderItems",
                        principalColumns: new[] { "Id", "OrderId", "VariantId", "FulfillmentKind" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShipmentItems_Shipments_ShipmentId_OrderId",
                        columns: x => new { x.ShipmentId, x.OrderId },
                        principalSchema: "Shipping",
                        principalTable: "Shipments",
                        principalColumns: new[] { "Id", "OrderId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShipmentItems_StockMovements_StockMovementId",
                        column: x => x.StockMovementId,
                        principalSchema: "Inventory",
                        principalTable: "StockMovements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_PhysicalStockReservation_Consumed",
                schema: "Ordering",
                table: "PhysicalStockReservations",
                sql: "\"ConsumedQuantity\" >= 0 AND \"ConsumedQuantity\" <= \"Quantity\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PhysicalStockReservation_State",
                schema: "Ordering",
                table: "PhysicalStockReservations",
                sql: "(\"State\" = 1 AND \"ConsumedQuantity\" < \"Quantity\") OR (\"State\" = 2 AND \"ConsumedQuantity\" = 0) OR (\"State\" = 3 AND \"ConsumedQuantity\" = \"Quantity\")");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PaymentReceipt_Source",
                schema: "Payments",
                table: "PaymentReceipts",
                sql: "\"Source\" IN ('ManualBankTransfer','ManualCOD')");

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentItems_OrderItemId_OrderId_VariantId_FulfillmentKind",
                schema: "Shipping",
                table: "ShipmentItems",
                columns: new[] { "OrderItemId", "OrderId", "VariantId", "FulfillmentKind" });

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentItems_ShipmentId_OrderId",
                schema: "Shipping",
                table: "ShipmentItems",
                columns: new[] { "ShipmentId", "OrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentItems_StockMovementId",
                schema: "Shipping",
                table: "ShipmentItems",
                column: "StockMovementId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_DeliveryOperationId",
                schema: "Shipping",
                table: "Shipments",
                column: "DeliveryOperationId",
                unique: true,
                filter: "[DeliveryOperationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_OperationId",
                schema: "Shipping",
                table: "Shipments",
                column: "OperationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_OrderId_DispatchedAtUtc_Id",
                schema: "Shipping",
                table: "Shipments",
                columns: new[] { "OrderId", "DispatchedAtUtc", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Stop before any DDL: older payment/ordering migrations also refuse these histories.
            // Keeping the latest applied migration intact makes a blocked downgrade observable and safe.
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [Shipping].[Shipments])
                   OR EXISTS (SELECT 1 FROM [Ordering].[PhysicalStockReservations] WHERE [ConsumedQuantity] > 0 OR [State] = 1)
                   OR EXISTS (SELECT 1 FROM [Ordering].[Orders] WHERE [PhysicalDispatchStarted] = 1 OR [PaymentStatus] = 2 OR [Status] = 5)
                   OR EXISTS (SELECT 1 FROM [Payments].[PaymentReceipts])
                   OR EXISTS (SELECT 1 FROM [Ordering].[DigitalDeliveries])
                   OR EXISTS (SELECT 1 FROM [DigitalInventory].[DigitalStockItems] WHERE [State] IN (3,4))
                   OR EXISTS (SELECT 1 FROM [Inventory].[InventoryItems] WHERE [Reserved] > 0)
                BEGIN
                    THROW 51000, 'PhysicalShipping cannot be rolled back while shipping/payment history or stock reservations exist. Preserve history with a forward migration or restore an earlier backup.', 1;
                END;
                """);

            migrationBuilder.DropTable(
                name: "ShipmentItems",
                schema: "Shipping");

            migrationBuilder.DropTable(
                name: "Shipments",
                schema: "Shipping");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PhysicalStockReservation_Consumed",
                schema: "Ordering",
                table: "PhysicalStockReservations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PhysicalStockReservation_State",
                schema: "Ordering",
                table: "PhysicalStockReservations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PaymentReceipt_Source",
                schema: "Payments",
                table: "PaymentReceipts");

            migrationBuilder.DropColumn(
                name: "ConsumedQuantity",
                schema: "Ordering",
                table: "PhysicalStockReservations");

            migrationBuilder.DropColumn(
                name: "PhysicalDispatchStarted",
                schema: "Ordering",
                table: "Orders");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PhysicalStockReservation_State",
                schema: "Ordering",
                table: "PhysicalStockReservations",
                sql: "\"State\" IN (1,2)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PaymentReceipt_Source",
                schema: "Payments",
                table: "PaymentReceipts",
                sql: "\"Source\" = 'ManualBankTransfer'");
        }
    }
}
