using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Boilerate.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class GuestStorefront : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsGuestOrder",
                schema: "Ordering",
                table: "Orders",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedEmail",
                schema: "Ordering",
                table: "Orders",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.Sql("UPDATE [Ordering].[Orders] SET [NormalizedEmail] = UPPER(LTRIM(RTRIM([Email]))) WHERE [NormalizedEmail] IS NULL");

            migrationBuilder.CreateTable(
                name: "GuestEmailChallenges",
                schema: "Ordering",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CodeHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConsumedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LastModifiedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Version = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuestEmailChallenges", x => x.Id);
                    table.CheckConstraint("CK_GuestEmailChallenge_Attempts", "\"Attempts\" BETWEEN 0 AND 5");
                });

            migrationBuilder.CreateTable(
                name: "StorefrontPaymentSettings",
                schema: "Ordering",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BankName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    AccountNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    AccountHolder = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    Branch = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    Instructions = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    BankTransferEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LastModifiedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Version = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StorefrontPaymentSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_NormalizedEmail_CreatedOn_Id",
                schema: "Ordering",
                table: "Orders",
                columns: new[] { "NormalizedEmail", "CreatedOn", "Id" },
                filter: "[IsGuestOrder] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_GuestEmailChallenges_NormalizedEmail_ConsumedAtUtc_ExpiresAtUtc",
                schema: "Ordering",
                table: "GuestEmailChallenges",
                columns: new[] { "NormalizedEmail", "ConsumedAtUtc", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_GuestEmailChallenges_NormalizedEmail_CreatedAtUtc",
                schema: "Ordering",
                table: "GuestEmailChallenges",
                columns: new[] { "NormalizedEmail", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GuestEmailChallenges",
                schema: "Ordering");

            migrationBuilder.DropTable(
                name: "StorefrontPaymentSettings",
                schema: "Ordering");

            migrationBuilder.DropIndex(
                name: "IX_Orders_NormalizedEmail_CreatedOn_Id",
                schema: "Ordering",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "IsGuestOrder",
                schema: "Ordering",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "NormalizedEmail",
                schema: "Ordering",
                table: "Orders");
        }
    }
}
