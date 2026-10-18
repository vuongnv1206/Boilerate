using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Boilerate.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class PaymentDigitalDelivery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Order_Status",
                schema: "Ordering",
                table: "Orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DigitalStockItem_State",
                schema: "DigitalInventory",
                table: "DigitalStockItems");

            migrationBuilder.EnsureSchema(
                name: "Payments");

            migrationBuilder.AddColumn<int>(
                name: "FulfillmentStatus",
                schema: "Ordering",
                table: "Orders",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<DateTime>(
                name: "PaidAtUtc",
                schema: "Ordering",
                table: "Orders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PaymentStatus",
                schema: "Ordering",
                table: "Orders",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_OrderItems_Id_OrderId_VariantId_FulfillmentKind",
                schema: "Ordering",
                table: "OrderItems",
                columns: new[] { "Id", "OrderId", "VariantId", "FulfillmentKind" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_DigitalStockItems_Id_VariantId_FulfillmentKind",
                schema: "DigitalInventory",
                table: "DigitalStockItems",
                columns: new[] { "Id", "VariantId", "FulfillmentKind" });

            migrationBuilder.CreateTable(
                name: "DigitalDeliveries",
                schema: "Ordering",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VariantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StockItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    FulfillmentKind = table.Column<int>(type: "int", nullable: false),
                    ProtectedContent = table.Column<string>(type: "nvarchar(max)", maxLength: 16000, nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LastModifiedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Version = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DigitalDeliveries", x => x.Id);
                    table.CheckConstraint("CK_DigitalDelivery_FulfillmentKind", "\"FulfillmentKind\" IN (2,3)");
                    table.CheckConstraint("CK_DigitalDelivery_Sequence", "\"Sequence\" BETWEEN 1 AND 999");
                    table.ForeignKey(
                        name: "FK_DigitalDeliveries_DigitalStockItems_StockItemId_VariantId_FulfillmentKind",
                        columns: x => new { x.StockItemId, x.VariantId, x.FulfillmentKind },
                        principalSchema: "DigitalInventory",
                        principalTable: "DigitalStockItems",
                        principalColumns: new[] { "Id", "VariantId", "FulfillmentKind" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DigitalDeliveries_OrderItems_OrderItemId_OrderId_VariantId_FulfillmentKind",
                        columns: x => new { x.OrderItemId, x.OrderId, x.VariantId, x.FulfillmentKind },
                        principalSchema: "Ordering",
                        principalTable: "OrderItems",
                        principalColumns: new[] { "Id", "OrderId", "VariantId", "FulfillmentKind" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaymentReceipts",
                schema: "Payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    Source = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    Reference = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    NormalizedReference = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "varchar(3)", unicode: false, maxLength: 3, nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LastModifiedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Version = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentReceipts", x => x.Id);
                    table.CheckConstraint("CK_PaymentReceipt_Amount", "\"Amount\" >= 0 AND \"Amount\" = FLOOR(\"Amount\") AND \"Currency\" = 'VND'");
                    table.CheckConstraint("CK_PaymentReceipt_Source", "\"Source\" = 'ManualBankTransfer'");
                    table.ForeignKey(
                        name: "FK_PaymentReceipts_Orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "Ordering",
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Order_Completion",
                schema: "Ordering",
                table: "Orders",
                sql: "\"Status\" <> 5 OR (\"PaymentStatus\" = 2 AND \"FulfillmentStatus\" = 4)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Order_FulfillmentStatus",
                schema: "Ordering",
                table: "Orders",
                sql: "\"FulfillmentStatus\" IN (1,2,3,4)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Order_PaidAt",
                schema: "Ordering",
                table: "Orders",
                sql: "(\"PaymentStatus\" = 1 AND \"PaidAtUtc\" IS NULL) OR (\"PaymentStatus\" = 2 AND \"PaidAtUtc\" IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Order_PaymentStatus",
                schema: "Ordering",
                table: "Orders",
                sql: "\"PaymentStatus\" IN (1,2)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Order_Status",
                schema: "Ordering",
                table: "Orders",
                sql: "\"Status\" IN (1,2,3,4,5)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DigitalStockItem_State",
                schema: "DigitalInventory",
                table: "DigitalStockItems",
                sql: "(\"State\" IN (1,2) AND \"ReservedForOrderItemId\" IS NULL) OR (\"State\" IN (3,4) AND \"FulfillmentKind\" = 2 AND \"ReservedForOrderItemId\" IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_DigitalDeliveries_OrderItemId_OrderId_VariantId_FulfillmentKind",
                schema: "Ordering",
                table: "DigitalDeliveries",
                columns: new[] { "OrderItemId", "OrderId", "VariantId", "FulfillmentKind" });

            migrationBuilder.CreateIndex(
                name: "IX_DigitalDeliveries_OrderItemId_Sequence",
                schema: "Ordering",
                table: "DigitalDeliveries",
                columns: new[] { "OrderItemId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DigitalDeliveries_StockItemId",
                schema: "Ordering",
                table: "DigitalDeliveries",
                column: "StockItemId",
                unique: true,
                filter: "[FulfillmentKind] = 2");

            migrationBuilder.CreateIndex(
                name: "IX_DigitalDeliveries_StockItemId_VariantId_FulfillmentKind",
                schema: "Ordering",
                table: "DigitalDeliveries",
                columns: new[] { "StockItemId", "VariantId", "FulfillmentKind" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentReceipts_OperationId",
                schema: "Payments",
                table: "PaymentReceipts",
                column: "OperationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentReceipts_OrderId",
                schema: "Payments",
                table: "PaymentReceipts",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentReceipts_Source_NormalizedReference",
                schema: "Payments",
                table: "PaymentReceipts",
                columns: new[] { "Source", "NormalizedReference" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Do not discard evidence of received money, delivered secrets or stock ownership.
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [Payments].[PaymentReceipts])
                   OR EXISTS (SELECT 1 FROM [Ordering].[DigitalDeliveries])
                   OR EXISTS (SELECT 1 FROM [Ordering].[Orders] WHERE [PaymentStatus] = 2 OR [Status] = 5)
                   OR EXISTS (SELECT 1 FROM [DigitalInventory].[DigitalStockItems] WHERE [State] IN (3,4))
                   OR EXISTS (SELECT 1 FROM [Inventory].[InventoryItems] WHERE [Reserved] > 0)
                   OR EXISTS (SELECT 1 FROM [Ordering].[PhysicalStockReservations] WHERE [State] = 1)
                BEGIN
                    THROW 51000, 'PaymentDigitalDelivery cannot be rolled back while payment history, delivered content or stock reservations exist. Preserve history with a forward migration or restore an earlier backup.', 1;
                END;
                """);

            migrationBuilder.DropTable(
                name: "DigitalDeliveries",
                schema: "Ordering");

            migrationBuilder.DropTable(
                name: "PaymentReceipts",
                schema: "Payments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Order_Completion",
                schema: "Ordering",
                table: "Orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Order_FulfillmentStatus",
                schema: "Ordering",
                table: "Orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Order_PaidAt",
                schema: "Ordering",
                table: "Orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Order_PaymentStatus",
                schema: "Ordering",
                table: "Orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Order_Status",
                schema: "Ordering",
                table: "Orders");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_OrderItems_Id_OrderId_VariantId_FulfillmentKind",
                schema: "Ordering",
                table: "OrderItems");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_DigitalStockItems_Id_VariantId_FulfillmentKind",
                schema: "DigitalInventory",
                table: "DigitalStockItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DigitalStockItem_State",
                schema: "DigitalInventory",
                table: "DigitalStockItems");

            migrationBuilder.DropColumn(
                name: "FulfillmentStatus",
                schema: "Ordering",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PaidAtUtc",
                schema: "Ordering",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PaymentStatus",
                schema: "Ordering",
                table: "Orders");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Order_Status",
                schema: "Ordering",
                table: "Orders",
                sql: "\"Status\" IN (1,2,3,4)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DigitalStockItem_State",
                schema: "DigitalInventory",
                table: "DigitalStockItems",
                sql: "(\"State\" IN (1,2) AND \"ReservedForOrderItemId\" IS NULL) OR (\"State\" = 3 AND \"FulfillmentKind\" = 2 AND \"ReservedForOrderItemId\" IS NOT NULL)");
        }
    }
}
