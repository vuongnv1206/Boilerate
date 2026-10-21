using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Boilerate.Migrators.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class GroupedAuthorization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "GroupId",
                schema: "Identity",
                table: "Functions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("cd9a1000-0000-4000-8000-000000000004"));

            migrationBuilder.CreateTable(
                name: "FunctionGroups",
                schema: "Identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NormalizedCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FunctionGroups", x => x.Id);
                    table.CheckConstraint("CK_FunctionGroups_SortOrder", "\"SortOrder\" >= 0");
                });

            migrationBuilder.InsertData(
                schema: "Identity",
                table: "FunctionGroups",
                columns: new[] { "Id", "Code", "Name", "NormalizedCode", "SortOrder", "Version" },
                values: new object[,]
                {
                    { new Guid("cd9a1000-0000-4000-8000-000000000001"), "System", "Hệ thống", "SYSTEM", 10, "initial" },
                    { new Guid("cd9a1000-0000-4000-8000-000000000002"), "Catalog", "Danh mục và sản phẩm", "CATALOG", 20, "initial" },
                    { new Guid("cd9a1000-0000-4000-8000-000000000003"), "Orders", "Đơn hàng", "ORDERS", 30, "initial" },
                    { new Guid("cd9a1000-0000-4000-8000-000000000004"), "Other", "Chức năng khác", "OTHER", 99, "initial" }
                });

            migrationBuilder.Sql(@"UPDATE ""Identity"".""Functions"" SET ""GroupId"" = CASE WHEN UPPER(""Code"") IN ('ACTION', 'FUNCTION', 'ROLE', 'USER', 'USERS', 'HANGFIRE', 'OUTBOX', 'NOTIFICATIONS') THEN 'cd9a1000-0000-4000-8000-000000000001'::uuid WHEN UPPER(""Code"") IN ('PRODUCT', 'PRODUCTS', 'CATEGORY', 'CATEGORIES', 'PRODUCTTYPE', 'PRICE', 'INVENTORY', 'DIGITALINVENTORY') THEN 'cd9a1000-0000-4000-8000-000000000002'::uuid WHEN UPPER(""Code"") IN ('ORDER', 'PAYMENT', 'SHIPPING', 'RENEWAL', 'AFTERSALES', 'REFUND', 'RETURN') THEN 'cd9a1000-0000-4000-8000-000000000003'::uuid ELSE 'cd9a1000-0000-4000-8000-000000000004'::uuid END");

            migrationBuilder.CreateIndex(
                name: "IX_Functions_GroupId",
                schema: "Identity",
                table: "Functions",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_FunctionGroups_NormalizedCode",
                schema: "Identity",
                table: "FunctionGroups",
                column: "NormalizedCode",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Functions_FunctionGroups_GroupId",
                schema: "Identity",
                table: "Functions",
                column: "GroupId",
                principalSchema: "Identity",
                principalTable: "FunctionGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Functions_FunctionGroups_GroupId",
                schema: "Identity",
                table: "Functions");

            migrationBuilder.DropTable(
                name: "FunctionGroups",
                schema: "Identity");

            migrationBuilder.DropIndex(
                name: "IX_Functions_GroupId",
                schema: "Identity",
                table: "Functions");

            migrationBuilder.DropColumn(
                name: "GroupId",
                schema: "Identity",
                table: "Functions");
        }
    }
}
