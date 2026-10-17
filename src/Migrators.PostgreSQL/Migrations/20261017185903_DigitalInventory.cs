using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Boilerate.Migrators.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class DigitalInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "DigitalInventory");

            migrationBuilder.AddColumn<int>(
                name: "FulfillmentKind",
                schema: "Catalog",
                table: "Products",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Products_Id_FulfillmentKind",
                schema: "Catalog",
                table: "Products",
                columns: new[] { "Id", "FulfillmentKind" });

            migrationBuilder.CreateTable(
                name: "DigitalStockItems",
                schema: "DigitalInventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    VariantId = table.Column<Guid>(type: "uuid", nullable: false),
                    FulfillmentKind = table.Column<int>(type: "integer", nullable: false),
                    Reference = table.Column<string>(type: "character varying(100)", unicode: false, maxLength: 100, nullable: false),
                    ProtectedContent = table.Column<string>(type: "character varying(16000)", maxLength: 16000, nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    LastModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DigitalStockItems", x => x.Id);
                    table.CheckConstraint("CK_DigitalStockItem_FulfillmentKind", "\"FulfillmentKind\" IN (2,3)");
                    table.CheckConstraint("CK_DigitalStockItem_State", "\"State\" IN (1,2)");
                    table.ForeignKey(
                        name: "FK_DigitalStockItems_ProductVariants_ProductId_VariantId",
                        columns: x => new { x.ProductId, x.VariantId },
                        principalSchema: "Catalog",
                        principalTable: "ProductVariants",
                        principalColumns: new[] { "ProductId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DigitalStockItems_Products_ProductId_FulfillmentKind",
                        columns: x => new { x.ProductId, x.FulfillmentKind },
                        principalSchema: "Catalog",
                        principalTable: "Products",
                        principalColumns: new[] { "Id", "FulfillmentKind" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Product_FulfillmentKind",
                schema: "Catalog",
                table: "Products",
                sql: "\"FulfillmentKind\" IN (1,2,3,4)");

            migrationBuilder.CreateIndex(
                name: "IX_DigitalStockItems_ProductId_FulfillmentKind",
                schema: "DigitalInventory",
                table: "DigitalStockItems",
                columns: new[] { "ProductId", "FulfillmentKind" });

            migrationBuilder.CreateIndex(
                name: "IX_DigitalStockItems_ProductId_VariantId_State",
                schema: "DigitalInventory",
                table: "DigitalStockItems",
                columns: new[] { "ProductId", "VariantId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_DigitalStockItems_VariantId",
                schema: "DigitalInventory",
                table: "DigitalStockItems",
                column: "VariantId",
                unique: true,
                filter: "\"FulfillmentKind\" = 3");

            migrationBuilder.CreateIndex(
                name: "IX_DigitalStockItems_VariantId_Reference",
                schema: "DigitalInventory",
                table: "DigitalStockItems",
                columns: new[] { "VariantId", "Reference" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DigitalStockItems",
                schema: "DigitalInventory");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Products_Id_FulfillmentKind",
                schema: "Catalog",
                table: "Products");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Product_FulfillmentKind",
                schema: "Catalog",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "FulfillmentKind",
                schema: "Catalog",
                table: "Products");
        }
    }
}
