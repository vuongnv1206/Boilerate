using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Boilerate.Migrators.PostgreSQL.Migrations
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
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedEmail",
                schema: "Ordering",
                table: "Orders",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.Sql("UPDATE \"Ordering\".\"Orders\" SET \"NormalizedEmail\" = UPPER(BTRIM(\"Email\")) WHERE \"NormalizedEmail\" IS NULL");

            migrationBuilder.CreateTable(
                name: "GuestEmailChallenges",
                schema: "Ordering",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CodeHash = table.Column<string>(type: "character varying(64)", unicode: false, maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSentAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ConsumedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    LastModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
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
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BankName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    AccountNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    AccountHolder = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    Branch = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    Instructions = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    BankTransferEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    LastModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
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
                filter: "\"IsGuestOrder\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_GuestEmailChallenges_NormalizedEmail_ConsumedAtUtc_ExpiresA~",
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
