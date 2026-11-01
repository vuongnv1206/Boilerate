using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Boilerate.Migrators.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class AdminOverviewIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Refunds_RefundedAtUtc_Id",
                schema: "Payments",
                table: "Refunds",
                columns: new[] { "RefundedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentReceipts_ReceivedAtUtc_Id",
                schema: "Payments",
                table: "PaymentReceipts",
                columns: new[] { "ReceivedAtUtc", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Refunds_RefundedAtUtc_Id",
                schema: "Payments",
                table: "Refunds");

            migrationBuilder.DropIndex(
                name: "IX_PaymentReceipts_ReceivedAtUtc_Id",
                schema: "Payments",
                table: "PaymentReceipts");
        }
    }
}
