using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Boilerate.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class OrderingCheckout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Inventory_Nonnegative",
                schema: "Inventory",
                table: "InventoryItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DigitalStockItem_State",
                schema: "DigitalInventory",
                table: "DigitalStockItems");

            migrationBuilder.EnsureSchema(
                name: "Ordering");

            migrationBuilder.AddColumn<int>(
                name: "Reserved",
                schema: "Inventory",
                table: "InventoryItems",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "ReservedForOrderItemId",
                schema: "DigitalInventory",
                table: "DigitalStockItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_InventoryItems_Id_VariantId",
                schema: "Inventory",
                table: "InventoryItems",
                columns: new[] { "Id", "VariantId" });

            migrationBuilder.CreateTable(
                name: "Carts",
                schema: "Ordering",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LastModifiedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Version = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Carts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CartItems",
                schema: "Ordering",
                columns: table => new
                {
                    CartId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VariantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CartItems", x => new { x.CartId, x.VariantId });
                    table.CheckConstraint("CK_CartItem_Quantity", "\"Quantity\" BETWEEN 1 AND 999");
                    table.ForeignKey(
                        name: "FK_CartItems_Carts_CartId",
                        column: x => x.CartId,
                        principalSchema: "Ordering",
                        principalTable: "Carts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CartItems_ProductVariants_VariantId",
                        column: x => x.VariantId,
                        principalSchema: "Catalog",
                        principalTable: "ProductVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Orders",
                schema: "Ordering",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CartId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    Number = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PaymentMethod = table.Column<int>(type: "int", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Subtotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ShippingAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    RecipientName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ShippingAddress = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LastModifiedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Version = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.Id);
                    table.CheckConstraint("CK_Order_Amounts", "\"Currency\" = 'VND' AND \"Subtotal\" >= 0 AND \"Subtotal\" = FLOOR(\"Subtotal\") AND \"ShippingAmount\" >= 0 AND \"ShippingAmount\" = FLOOR(\"ShippingAmount\") AND \"Total\" >= 0 AND \"Total\" = FLOOR(\"Total\") AND \"Total\" = \"Subtotal\" + \"ShippingAmount\"");
                    table.CheckConstraint("CK_Order_Expiry", "(\"Status\" <> 1 OR \"ExpiresAt\" IS NOT NULL) AND (\"PaymentMethod\" <> 2 OR \"ExpiresAt\" IS NULL)");
                    table.CheckConstraint("CK_Order_PaymentMethod", "\"PaymentMethod\" IN (1,2)");
                    table.CheckConstraint("CK_Order_Status", "\"Status\" IN (1,2,3,4)");
                    table.ForeignKey(
                        name: "FK_Orders_Carts_CartId",
                        column: x => x.CartId,
                        principalSchema: "Ordering",
                        principalTable: "Carts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrderItems",
                schema: "Ordering",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VariantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Sku = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OptionDescription = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    FulfillmentKind = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderItems", x => x.Id);
                    table.UniqueConstraint("AK_OrderItems_Id_VariantId", x => new { x.Id, x.VariantId });
                    table.CheckConstraint("CK_OrderItem_FulfillmentKind", "\"FulfillmentKind\" IN (1,2,3,4)");
                    table.CheckConstraint("CK_OrderItem_Price", "\"UnitPrice\" >= 0 AND \"UnitPrice\" = FLOOR(\"UnitPrice\")");
                    table.CheckConstraint("CK_OrderItem_Quantity", "\"Quantity\" BETWEEN 1 AND 999");
                    table.ForeignKey(
                        name: "FK_OrderItems_Orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "Ordering",
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderItems_ProductVariants_ProductId_VariantId",
                        columns: x => new { x.ProductId, x.VariantId },
                        principalSchema: "Catalog",
                        principalTable: "ProductVariants",
                        principalColumns: new[] { "ProductId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PhysicalStockReservations",
                schema: "Ordering",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VariantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    State = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LastModifiedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Version = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhysicalStockReservations", x => x.Id);
                    table.CheckConstraint("CK_PhysicalStockReservation_Quantity", "\"Quantity\" > 0");
                    table.CheckConstraint("CK_PhysicalStockReservation_State", "\"State\" IN (1,2)");
                    table.ForeignKey(
                        name: "FK_PhysicalStockReservations_InventoryItems_InventoryItemId_VariantId",
                        columns: x => new { x.InventoryItemId, x.VariantId },
                        principalSchema: "Inventory",
                        principalTable: "InventoryItems",
                        principalColumns: new[] { "Id", "VariantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PhysicalStockReservations_OrderItems_OrderItemId_VariantId",
                        columns: x => new { x.OrderItemId, x.VariantId },
                        principalSchema: "Ordering",
                        principalTable: "OrderItems",
                        principalColumns: new[] { "Id", "VariantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "Catalog",
                table: "CommerceLocks",
                columns: new[] { "Name", "Version" },
                values: new object[] { "ordering", "initial" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Inventory_Nonnegative",
                schema: "Inventory",
                table: "InventoryItems",
                sql: "\"Reserved\" >= 0 AND \"OnHand\" >= \"Reserved\"");

            migrationBuilder.CreateIndex(
                name: "IX_DigitalStockItems_ReservedForOrderItemId_VariantId",
                schema: "DigitalInventory",
                table: "DigitalStockItems",
                columns: new[] { "ReservedForOrderItemId", "VariantId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_DigitalStockItem_State",
                schema: "DigitalInventory",
                table: "DigitalStockItems",
                sql: "(\"State\" IN (1,2) AND \"ReservedForOrderItemId\" IS NULL) OR (\"State\" = 3 AND \"FulfillmentKind\" = 2 AND \"ReservedForOrderItemId\" IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_CartItems_VariantId",
                schema: "Ordering",
                table: "CartItems",
                column: "VariantId");

            migrationBuilder.CreateIndex(
                name: "IX_Carts_CustomerId",
                schema: "Ordering",
                table: "Carts",
                column: "CustomerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_OrderId_VariantId",
                schema: "Ordering",
                table: "OrderItems",
                columns: new[] { "OrderId", "VariantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_ProductId_VariantId",
                schema: "Ordering",
                table: "OrderItems",
                columns: new[] { "ProductId", "VariantId" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_CartId",
                schema: "Ordering",
                table: "Orders",
                column: "CartId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_CustomerId_CreatedOn_Id",
                schema: "Ordering",
                table: "Orders",
                columns: new[] { "CustomerId", "CreatedOn", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_CustomerId_OperationId",
                schema: "Ordering",
                table: "Orders",
                columns: new[] { "CustomerId", "OperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Number",
                schema: "Ordering",
                table: "Orders",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Status_ExpiresAt",
                schema: "Ordering",
                table: "Orders",
                columns: new[] { "Status", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalStockReservations_InventoryItemId_VariantId",
                schema: "Ordering",
                table: "PhysicalStockReservations",
                columns: new[] { "InventoryItemId", "VariantId" });

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalStockReservations_OrderItemId",
                schema: "Ordering",
                table: "PhysicalStockReservations",
                column: "OrderItemId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalStockReservations_OrderItemId_VariantId",
                schema: "Ordering",
                table: "PhysicalStockReservations",
                columns: new[] { "OrderItemId", "VariantId" });

            migrationBuilder.AddForeignKey(
                name: "FK_DigitalStockItems_OrderItems_ReservedForOrderItemId_VariantId",
                schema: "DigitalInventory",
                table: "DigitalStockItems",
                columns: new[] { "ReservedForOrderItemId", "VariantId" },
                principalSchema: "Ordering",
                principalTable: "OrderItems",
                principalColumns: new[] { "Id", "VariantId" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Refuse to remove ownership/balance data while any stock is still held.
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [Inventory].[InventoryItems] WHERE [Reserved] > 0)
                   OR EXISTS (SELECT 1 FROM [Ordering].[PhysicalStockReservations] WHERE [State] = 1)
                   OR EXISTS (SELECT 1 FROM [DigitalInventory].[DigitalStockItems] WHERE [State] = 3)
                BEGIN
                    THROW 51000, 'Release all physical and digital reservations before rolling back OrderingCheckout.', 1;
                END;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_DigitalStockItems_OrderItems_ReservedForOrderItemId_VariantId",
                schema: "DigitalInventory",
                table: "DigitalStockItems");

            migrationBuilder.DropTable(
                name: "CartItems",
                schema: "Ordering");

            migrationBuilder.DropTable(
                name: "PhysicalStockReservations",
                schema: "Ordering");

            migrationBuilder.DropTable(
                name: "OrderItems",
                schema: "Ordering");

            migrationBuilder.DropTable(
                name: "Orders",
                schema: "Ordering");

            migrationBuilder.DropTable(
                name: "Carts",
                schema: "Ordering");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_InventoryItems_Id_VariantId",
                schema: "Inventory",
                table: "InventoryItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Inventory_Nonnegative",
                schema: "Inventory",
                table: "InventoryItems");

            migrationBuilder.DropIndex(
                name: "IX_DigitalStockItems_ReservedForOrderItemId_VariantId",
                schema: "DigitalInventory",
                table: "DigitalStockItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DigitalStockItem_State",
                schema: "DigitalInventory",
                table: "DigitalStockItems");

            migrationBuilder.DeleteData(
                schema: "Catalog",
                table: "CommerceLocks",
                keyColumn: "Name",
                keyValue: "ordering");

            migrationBuilder.DropColumn(
                name: "Reserved",
                schema: "Inventory",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "ReservedForOrderItemId",
                schema: "DigitalInventory",
                table: "DigitalStockItems");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Inventory_Nonnegative",
                schema: "Inventory",
                table: "InventoryItems",
                sql: "\"OnHand\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DigitalStockItem_State",
                schema: "DigitalInventory",
                table: "DigitalStockItems",
                sql: "\"State\" IN (1,2)");
        }
    }
}
