using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Boilerate.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class DiscountCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Order_Amounts",
                schema: "Ordering",
                table: "Orders");

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                schema: "Ordering",
                table: "Orders",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "DiscountCode",
                schema: "Ordering",
                table: "Orders",
                type: "varchar(32)",
                unicode: false,
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DiscountId",
                schema: "Ordering",
                table: "Orders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DiscountKind",
                schema: "Ordering",
                table: "Orders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DiscountUsageHeld",
                schema: "Ordering",
                table: "Orders",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountValue",
                schema: "Ordering",
                table: "Orders",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                schema: "Ordering",
                table: "OrderItems",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "DiscountCode",
                schema: "Ordering",
                table: "Carts",
                type: "varchar(32)",
                unicode: false,
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Discounts",
                schema: "Ordering",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MinimumSubtotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    StartsAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndsAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UsageLimit = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LastModifiedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Version = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Discounts", x => x.Id);
                    table.CheckConstraint("CK_Discount_KindValue", "(\"Kind\" = 1 AND \"Value\" BETWEEN 1 AND 99) OR (\"Kind\" = 2 AND \"Value\" >= 1 AND \"Value\" <= 9007199254740991)");
                    table.CheckConstraint("CK_Discount_LimitPeriod", "(\"UsageLimit\" IS NULL OR \"UsageLimit\" > 0) AND (\"StartsAtUtc\" IS NULL OR \"EndsAtUtc\" IS NULL OR \"StartsAtUtc\" < \"EndsAtUtc\")");
                    table.CheckConstraint("CK_Discount_WholeValues", "\"Value\" = FLOOR(\"Value\") AND \"MinimumSubtotal\" >= 0 AND \"MinimumSubtotal\" <= 9007199254740991 AND \"MinimumSubtotal\" = FLOOR(\"MinimumSubtotal\")");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_DiscountId_DiscountUsageHeld",
                schema: "Ordering",
                table: "Orders",
                columns: new[] { "DiscountId", "DiscountUsageHeld" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Order_Amounts",
                schema: "Ordering",
                table: "Orders",
                sql: "\"Currency\" = 'VND' AND \"Subtotal\" >= 0 AND \"Subtotal\" = FLOOR(\"Subtotal\") AND \"ShippingAmount\" >= 0 AND \"ShippingAmount\" = FLOOR(\"ShippingAmount\") AND \"DiscountAmount\" >= 0 AND \"DiscountAmount\" = FLOOR(\"DiscountAmount\") AND \"DiscountAmount\" <= \"Subtotal\" AND \"Total\" >= 0 AND \"Total\" = FLOOR(\"Total\") AND \"Total\" = \"Subtotal\" + \"ShippingAmount\" - \"DiscountAmount\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Order_Discount",
                schema: "Ordering",
                table: "Orders",
                sql: "(\"DiscountId\" IS NULL AND \"DiscountCode\" IS NULL AND \"DiscountKind\" IS NULL AND \"DiscountValue\" IS NULL AND \"DiscountAmount\" = 0) OR (\"DiscountId\" IS NOT NULL AND \"DiscountCode\" IS NOT NULL AND \"DiscountKind\" IN (1,2) AND \"DiscountValue\" IS NOT NULL AND \"DiscountAmount\" > 0 AND \"DiscountAmount\" < \"Subtotal\")");

            migrationBuilder.AddCheckConstraint(
                name: "CK_OrderItem_Discount",
                schema: "Ordering",
                table: "OrderItems",
                sql: "\"DiscountAmount\" >= 0 AND \"DiscountAmount\" = FLOOR(\"DiscountAmount\") AND \"DiscountAmount\" <= \"UnitPrice\" * \"Quantity\"");

            migrationBuilder.CreateIndex(
                name: "IX_Discounts_Code",
                schema: "Ordering",
                table: "Discounts",
                column: "Code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_Discounts_DiscountId",
                schema: "Ordering",
                table: "Orders",
                column: "DiscountId",
                principalSchema: "Ordering",
                principalTable: "Discounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [Ordering].[Discounts]) OR EXISTS (SELECT 1 FROM [Ordering].[Orders] WHERE [DiscountId] IS NOT NULL) OR EXISTS (SELECT 1 FROM [Ordering].[Carts] WHERE [DiscountCode] IS NOT NULL) THROW 51000, 'Cannot remove discount history or active cart codes.', 1;");
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_Discounts_DiscountId",
                schema: "Ordering",
                table: "Orders");

            migrationBuilder.DropTable(
                name: "Discounts",
                schema: "Ordering");

            migrationBuilder.DropIndex(
                name: "IX_Orders_DiscountId_DiscountUsageHeld",
                schema: "Ordering",
                table: "Orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Order_Amounts",
                schema: "Ordering",
                table: "Orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Order_Discount",
                schema: "Ordering",
                table: "Orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_OrderItem_Discount",
                schema: "Ordering",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                schema: "Ordering",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DiscountCode",
                schema: "Ordering",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DiscountId",
                schema: "Ordering",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DiscountKind",
                schema: "Ordering",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DiscountUsageHeld",
                schema: "Ordering",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DiscountValue",
                schema: "Ordering",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                schema: "Ordering",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "DiscountCode",
                schema: "Ordering",
                table: "Carts");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Order_Amounts",
                schema: "Ordering",
                table: "Orders",
                sql: "\"Currency\" = 'VND' AND \"Subtotal\" >= 0 AND \"Subtotal\" = FLOOR(\"Subtotal\") AND \"ShippingAmount\" >= 0 AND \"ShippingAmount\" = FLOOR(\"ShippingAmount\") AND \"Total\" >= 0 AND \"Total\" = FLOOR(\"Total\") AND \"Total\" = \"Subtotal\" + \"ShippingAmount\"");
        }
    }
}
