using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Boilerate.Migrators.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class WebflowCatalogEditor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProductVariants_NormalizedSku",
                schema: "Catalog",
                table: "ProductVariants");

            migrationBuilder.DropIndex(
                name: "IX_ProductVariants_ProductId_CombinationKey",
                schema: "Catalog",
                table: "ProductVariants");

            migrationBuilder.AlterColumn<Guid>(
                name: "StockMovementId",
                schema: "Shipping",
                table: "ShipmentItems",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<decimal>(
                name: "Height",
                schema: "Catalog",
                table: "ProductVariants",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRetired",
                schema: "Catalog",
                table: "ProductVariants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "Length",
                schema: "Catalog",
                table: "ProductVariants",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TrackInventory",
                schema: "Catalog",
                table: "ProductVariants",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Weight",
                schema: "Catalog",
                table: "ProductVariants",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Width",
                schema: "Catalog",
                table: "ProductVariants",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                schema: "Catalog",
                table: "Products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsRetired",
                schema: "Catalog",
                table: "ProductOptionValues",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsRetired",
                schema: "Catalog",
                table: "ProductOptions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "TrackInventory",
                schema: "Ordering",
                table: "OrderItems",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "VariantMedia",
                schema: "Catalog",
                columns: table => new
                {
                    VariantId = table.Column<Guid>(type: "uuid", nullable: false),
                    MediaAssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    AltText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VariantMedia", x => new { x.VariantId, x.MediaAssetId });
                    table.ForeignKey(
                        name: "FK_VariantMedia_MediaAssets_MediaAssetId",
                        column: x => x.MediaAssetId,
                        principalSchema: "Media",
                        principalTable: "MediaAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VariantMedia_ProductVariants_VariantId",
                        column: x => x.VariantId,
                        principalSchema: "Catalog",
                        principalTable: "ProductVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariants_NormalizedSku",
                schema: "Catalog",
                table: "ProductVariants",
                column: "NormalizedSku");

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariants_ProductId_CombinationKey",
                schema: "Catalog",
                table: "ProductVariants",
                columns: new[] { "ProductId", "CombinationKey" },
                unique: true,
                filter: "\"IsRetired\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_VariantMedia_MediaAssetId",
                schema: "Catalog",
                table: "VariantMedia",
                column: "MediaAssetId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM "Catalog"."VariantMedia")
                    OR EXISTS (SELECT 1 FROM "Catalog"."Products" WHERE "IsDeleted")
                    OR EXISTS (SELECT 1 FROM "Catalog"."ProductOptions" WHERE "IsRetired")
                    OR EXISTS (SELECT 1 FROM "Catalog"."ProductOptionValues" WHERE "IsRetired")
                    OR EXISTS (SELECT 1 FROM "Catalog"."ProductVariants" WHERE "IsRetired" OR NOT "TrackInventory" OR "Weight" IS NOT NULL OR "Width" IS NOT NULL OR "Height" IS NOT NULL OR "Length" IS NOT NULL OR "Sku" = '')
                    OR EXISTS (SELECT "NormalizedSku" FROM "Catalog"."ProductVariants" GROUP BY "NormalizedSku" HAVING COUNT(*) > 1)
                    OR EXISTS (SELECT 1 FROM "Ordering"."OrderItems" WHERE NOT "TrackInventory")
                    OR EXISTS (SELECT 1 FROM "Shipping"."ShipmentItems" WHERE "StockMovementId" IS NULL)
                    OR EXISTS (SELECT 1 FROM "Payments"."Refunds")
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
                  THEN RAISE EXCEPTION 'Catalog or commerce history must be explicitly reconciled before downgrading.'; END IF;
                END $$;
                """);

            migrationBuilder.DropTable(
                name: "VariantMedia",
                schema: "Catalog");

            migrationBuilder.DropIndex(
                name: "IX_ProductVariants_NormalizedSku",
                schema: "Catalog",
                table: "ProductVariants");

            migrationBuilder.DropIndex(
                name: "IX_ProductVariants_ProductId_CombinationKey",
                schema: "Catalog",
                table: "ProductVariants");

            migrationBuilder.DropColumn(
                name: "Height",
                schema: "Catalog",
                table: "ProductVariants");

            migrationBuilder.DropColumn(
                name: "IsRetired",
                schema: "Catalog",
                table: "ProductVariants");

            migrationBuilder.DropColumn(
                name: "Length",
                schema: "Catalog",
                table: "ProductVariants");

            migrationBuilder.DropColumn(
                name: "TrackInventory",
                schema: "Catalog",
                table: "ProductVariants");

            migrationBuilder.DropColumn(
                name: "Weight",
                schema: "Catalog",
                table: "ProductVariants");

            migrationBuilder.DropColumn(
                name: "Width",
                schema: "Catalog",
                table: "ProductVariants");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                schema: "Catalog",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "IsRetired",
                schema: "Catalog",
                table: "ProductOptionValues");

            migrationBuilder.DropColumn(
                name: "IsRetired",
                schema: "Catalog",
                table: "ProductOptions");

            migrationBuilder.DropColumn(
                name: "TrackInventory",
                schema: "Ordering",
                table: "OrderItems");

            migrationBuilder.AlterColumn<Guid>(
                name: "StockMovementId",
                schema: "Shipping",
                table: "ShipmentItems",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariants_NormalizedSku",
                schema: "Catalog",
                table: "ProductVariants",
                column: "NormalizedSku",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariants_ProductId_CombinationKey",
                schema: "Catalog",
                table: "ProductVariants",
                columns: new[] { "ProductId", "CombinationKey" },
                unique: true);
        }
    }
}
