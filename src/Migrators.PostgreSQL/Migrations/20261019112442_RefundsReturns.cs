using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Boilerate.Migrators.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class RefundsReturns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PhysicalStockReservation_State",
                schema: "Ordering",
                table: "PhysicalStockReservations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Order_FulfillmentStatus",
                schema: "Ordering",
                table: "Orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Order_Status",
                schema: "Ordering",
                table: "Orders");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_ShipmentItems_ShipmentId_OrderItemId_OrderId_VariantId",
                schema: "Shipping",
                table: "ShipmentItems",
                columns: new[] { "ShipmentId", "OrderItemId", "OrderId", "VariantId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_PaymentReceipts_Id_OrderId_Currency",
                schema: "Payments",
                table: "PaymentReceipts",
                columns: new[] { "Id", "OrderId", "Currency" });

            migrationBuilder.CreateTable(
                name: "PhysicalReturns",
                schema: "Shipping",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestHash = table.Column<string>(type: "character varying(64)", unicode: false, maxLength: 64, nullable: false),
                    Reference = table.Column<string>(type: "character varying(100)", unicode: false, maxLength: 100, nullable: false),
                    NormalizedReference = table.Column<string>(type: "character varying(100)", unicode: false, maxLength: 100, nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CloseRemainingCod = table.Column<bool>(type: "boolean", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    LastModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhysicalReturns", x => x.Id);
                    table.UniqueConstraint("AK_PhysicalReturns_Id_OrderId", x => new { x.Id, x.OrderId });
                    table.ForeignKey(
                        name: "FK_PhysicalReturns_Orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "Ordering",
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Refunds",
                schema: "Payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceiptId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestHash = table.Column<string>(type: "character varying(64)", unicode: false, maxLength: 64, nullable: false),
                    Reference = table.Column<string>(type: "character varying(100)", unicode: false, maxLength: 100, nullable: false),
                    NormalizedReference = table.Column<string>(type: "character varying(100)", unicode: false, maxLength: 100, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", unicode: false, maxLength: 3, nullable: false),
                    RefundedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Source = table.Column<string>(type: "character varying(32)", unicode: false, maxLength: 32, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    LastModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Refunds", x => x.Id);
                    table.CheckConstraint("CK_Refund_Amount", "\"Amount\" > 0 AND \"Amount\" = FLOOR(\"Amount\") AND \"Currency\" = 'VND'");
                    table.CheckConstraint("CK_Refund_Source", "\"Source\" IN ('ManualBankTransfer','ManualCash')");
                    table.ForeignKey(
                        name: "FK_Refunds_PaymentReceipts_ReceiptId_OrderId_Currency",
                        columns: x => new { x.ReceiptId, x.OrderId, x.Currency },
                        principalSchema: "Payments",
                        principalTable: "PaymentReceipts",
                        principalColumns: new[] { "Id", "OrderId", "Currency" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PhysicalReturnItems",
                schema: "Shipping",
                columns: table => new
                {
                    ReturnId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShipmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    VariantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    RestockQuantity = table.Column<int>(type: "integer", nullable: false),
                    StockMovementId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhysicalReturnItems", x => new { x.ReturnId, x.ShipmentId, x.OrderItemId });
                    table.CheckConstraint("CK_PhysicalReturnItem_Movement", "(\"RestockQuantity\" = 0 AND \"StockMovementId\" IS NULL) OR (\"RestockQuantity\" > 0 AND \"StockMovementId\" IS NOT NULL)");
                    table.CheckConstraint("CK_PhysicalReturnItem_Quantity", "\"Quantity\" BETWEEN 1 AND 999 AND \"RestockQuantity\" >= 0 AND \"RestockQuantity\" <= \"Quantity\"");
                    table.ForeignKey(
                        name: "FK_PhysicalReturnItems_PhysicalReturns_ReturnId_OrderId",
                        columns: x => new { x.ReturnId, x.OrderId },
                        principalSchema: "Shipping",
                        principalTable: "PhysicalReturns",
                        principalColumns: new[] { "Id", "OrderId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PhysicalReturnItems_ShipmentItems_ShipmentId_OrderItemId_Or~",
                        columns: x => new { x.ShipmentId, x.OrderItemId, x.OrderId, x.VariantId },
                        principalSchema: "Shipping",
                        principalTable: "ShipmentItems",
                        principalColumns: new[] { "ShipmentId", "OrderItemId", "OrderId", "VariantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PhysicalReturnItems_StockMovements_StockMovementId",
                        column: x => x.StockMovementId,
                        principalSchema: "Inventory",
                        principalTable: "StockMovements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_PhysicalStockReservation_State",
                schema: "Ordering",
                table: "PhysicalStockReservations",
                sql: "(\"State\" IN (1,4) AND \"ConsumedQuantity\" < \"Quantity\") OR (\"State\" = 2 AND \"ConsumedQuantity\" = 0) OR (\"State\" = 3 AND \"ConsumedQuantity\" = \"Quantity\")");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Order_Closure",
                schema: "Ordering",
                table: "Orders",
                sql: "(\"Status\" = 6 AND \"FulfillmentStatus\" = 5 AND (\"PaymentStatus\" = 2 OR \"PaymentMethod\" = 2)) OR (\"Status\" <> 6 AND \"FulfillmentStatus\" <> 5)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Order_FulfillmentStatus",
                schema: "Ordering",
                table: "Orders",
                sql: "\"FulfillmentStatus\" IN (1,2,3,4,5)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Order_Status",
                schema: "Ordering",
                table: "Orders",
                sql: "\"Status\" IN (1,2,3,4,5,6)");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalReturnItems_ReturnId_OrderId",
                schema: "Shipping",
                table: "PhysicalReturnItems",
                columns: new[] { "ReturnId", "OrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalReturnItems_ShipmentId_OrderItemId_OrderId_VariantId",
                schema: "Shipping",
                table: "PhysicalReturnItems",
                columns: new[] { "ShipmentId", "OrderItemId", "OrderId", "VariantId" });

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalReturnItems_StockMovementId",
                schema: "Shipping",
                table: "PhysicalReturnItems",
                column: "StockMovementId",
                unique: true,
                filter: "\"StockMovementId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalReturns_NormalizedReference",
                schema: "Shipping",
                table: "PhysicalReturns",
                column: "NormalizedReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalReturns_OperationId",
                schema: "Shipping",
                table: "PhysicalReturns",
                column: "OperationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalReturns_OrderId_ReceivedAtUtc_Id",
                schema: "Shipping",
                table: "PhysicalReturns",
                columns: new[] { "OrderId", "ReceivedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_OperationId",
                schema: "Payments",
                table: "Refunds",
                column: "OperationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_OrderId_RefundedAtUtc_Id",
                schema: "Payments",
                table: "Refunds",
                columns: new[] { "OrderId", "RefundedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_ReceiptId_OrderId_Currency",
                schema: "Payments",
                table: "Refunds",
                columns: new[] { "ReceiptId", "OrderId", "Currency" });

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_Source_NormalizedReference",
                schema: "Payments",
                table: "Refunds",
                columns: new[] { "Source", "NormalizedReference" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Refuse before any DDL so a blocked older downgrade retains the latest schema.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "Payments"."Refunds")
                       OR EXISTS (SELECT 1 FROM "Shipping"."PhysicalReturns")
                       OR EXISTS (SELECT 1 FROM "Shipping"."PhysicalReturnItems")
                       OR EXISTS (SELECT 1 FROM "Ordering"."Orders" WHERE "Status" = 6 OR "FulfillmentStatus" = 5)
                       OR EXISTS (SELECT 1 FROM "Ordering"."PhysicalStockReservations" WHERE "State" = 4)
                       OR EXISTS (SELECT 1 FROM "Renewals"."RenewalRequests")
                       OR EXISTS (SELECT 1 FROM "Renewals"."RenewalAttempts")
                       OR EXISTS (SELECT 1 FROM "Renewals"."RenewalOperations")
                       OR EXISTS (SELECT 1 FROM "Shipping"."Shipments")
                       OR EXISTS (SELECT 1 FROM "Ordering"."PhysicalStockReservations" WHERE "ConsumedQuantity" > 0 OR "State" = 1)
                       OR EXISTS (SELECT 1 FROM "Ordering"."Orders" WHERE "PhysicalDispatchStarted" OR "PaymentStatus" = 2 OR "Status" = 5)
                       OR EXISTS (SELECT 1 FROM "Payments"."PaymentReceipts")
                       OR EXISTS (SELECT 1 FROM "Ordering"."DigitalDeliveries")
                       OR EXISTS (SELECT 1 FROM "DigitalInventory"."DigitalStockItems" WHERE "State" IN (3,4))
                       OR EXISTS (SELECT 1 FROM "Inventory"."InventoryItems" WHERE "Reserved" > 0)
                    THEN
                        RAISE EXCEPTION 'RefundsReturns cannot be rolled back while after-sales/renewal/shipping/payment history or stock reservations exist. Preserve history with a forward migration or restore an earlier backup.';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropTable(
                name: "PhysicalReturnItems",
                schema: "Shipping");

            migrationBuilder.DropTable(
                name: "Refunds",
                schema: "Payments");

            migrationBuilder.DropTable(
                name: "PhysicalReturns",
                schema: "Shipping");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_ShipmentItems_ShipmentId_OrderItemId_OrderId_VariantId",
                schema: "Shipping",
                table: "ShipmentItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PhysicalStockReservation_State",
                schema: "Ordering",
                table: "PhysicalStockReservations");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_PaymentReceipts_Id_OrderId_Currency",
                schema: "Payments",
                table: "PaymentReceipts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Order_Closure",
                schema: "Ordering",
                table: "Orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Order_FulfillmentStatus",
                schema: "Ordering",
                table: "Orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Order_Status",
                schema: "Ordering",
                table: "Orders");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PhysicalStockReservation_State",
                schema: "Ordering",
                table: "PhysicalStockReservations",
                sql: "(\"State\" = 1 AND \"ConsumedQuantity\" < \"Quantity\") OR (\"State\" = 2 AND \"ConsumedQuantity\" = 0) OR (\"State\" = 3 AND \"ConsumedQuantity\" = \"Quantity\")");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Order_FulfillmentStatus",
                schema: "Ordering",
                table: "Orders",
                sql: "\"FulfillmentStatus\" IN (1,2,3,4)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Order_Status",
                schema: "Ordering",
                table: "Orders",
                sql: "\"Status\" IN (1,2,3,4,5)");
        }
    }
}
