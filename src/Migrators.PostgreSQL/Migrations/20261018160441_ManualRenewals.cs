using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Boilerate.Migrators.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class ManualRenewals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "Renewals");

            migrationBuilder.CreateTable(
                name: "RenewalRequests",
                schema: "Renewals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    VariantId = table.Column<Guid>(type: "uuid", nullable: false),
                    FulfillmentKind = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ProtectedTarget = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    TargetKey = table.Column<string>(type: "character varying(64)", unicode: false, maxLength: 64, nullable: true),
                    TargetPreview = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TargetLocked = table.Column<bool>(type: "boolean", nullable: false),
                    ClaimedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ClaimToken = table.Column<Guid>(type: "uuid", nullable: true),
                    ClaimUntilUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VerifiedExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    LastModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RenewalRequests", x => x.Id);
                    table.CheckConstraint("CK_RenewalRequest_Claim", "(\"Status\" = 3 AND \"ClaimedBy\" IS NOT NULL AND \"ClaimToken\" IS NOT NULL AND \"ClaimUntilUtc\" IS NOT NULL) OR (\"Status\" = 4 AND \"ClaimedBy\" IS NOT NULL AND \"ClaimToken\" IS NULL AND \"ClaimUntilUtc\" IS NULL) OR (\"Status\" IN (1,2,5) AND \"ClaimedBy\" IS NULL AND \"ClaimToken\" IS NULL AND \"ClaimUntilUtc\" IS NULL)");
                    table.CheckConstraint("CK_RenewalRequest_Completion", "(\"Status\" = 5 AND \"CompletedAtUtc\" IS NOT NULL AND \"VerifiedExpiresAtUtc\" IS NOT NULL AND \"VerifiedExpiresAtUtc\" > \"CompletedAtUtc\") OR (\"Status\" <> 5 AND \"CompletedAtUtc\" IS NULL AND \"VerifiedExpiresAtUtc\" IS NULL)");
                    table.CheckConstraint("CK_RenewalRequest_Kind", "\"FulfillmentKind\" = 4");
                    table.CheckConstraint("CK_RenewalRequest_Status", "\"Status\" IN (1,2,3,4,5)");
                    table.CheckConstraint("CK_RenewalRequest_Target", "(\"Status\" = 1 AND \"ProtectedTarget\" IS NULL AND \"TargetKey\" IS NULL AND \"TargetPreview\" IS NULL) OR (\"Status\" <> 1 AND \"ProtectedTarget\" IS NOT NULL AND \"TargetKey\" IS NOT NULL AND \"TargetPreview\" IS NOT NULL)");
                    table.CheckConstraint("CK_RenewalRequest_TargetLock", "(\"Status\" <> 1 OR NOT \"TargetLocked\") AND (\"Status\" NOT IN (4,5) OR \"TargetLocked\")");
                    table.ForeignKey(
                        name: "FK_RenewalRequests_OrderItems_OrderItemId_OrderId_VariantId_Fu~",
                        columns: x => new { x.OrderItemId, x.OrderId, x.VariantId, x.FulfillmentKind },
                        principalSchema: "Ordering",
                        principalTable: "OrderItems",
                        principalColumns: new[] { "Id", "OrderId", "VariantId", "FulfillmentKind" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RenewalAttempts",
                schema: "Renewals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Outcome = table.Column<int>(type: "integer", nullable: false),
                    VerifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    VerifiedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NewExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProtectedEvidence = table.Column<string>(type: "character varying(32000)", maxLength: 32000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RenewalAttempts", x => x.Id);
                    table.CheckConstraint("CK_RenewalAttempt_Outcome", "\"Outcome\" IN (1,2,3)");
                    table.CheckConstraint("CK_RenewalAttempt_Verification", "(\"Outcome\" = 1 AND \"VerifiedBy\" IS NULL AND \"VerifiedAtUtc\" IS NULL AND \"NewExpiresAtUtc\" IS NULL AND \"ProtectedEvidence\" IS NULL) OR (\"Outcome\" = 2 AND \"VerifiedBy\" IS NOT NULL AND \"VerifiedAtUtc\" IS NOT NULL AND \"VerifiedAtUtc\" >= \"StartedAtUtc\" AND \"NewExpiresAtUtc\" IS NOT NULL AND \"NewExpiresAtUtc\" > \"VerifiedAtUtc\" AND \"ProtectedEvidence\" IS NOT NULL) OR (\"Outcome\" = 3 AND \"VerifiedBy\" IS NOT NULL AND \"VerifiedAtUtc\" IS NOT NULL AND \"VerifiedAtUtc\" >= \"StartedAtUtc\" AND \"NewExpiresAtUtc\" IS NULL AND \"ProtectedEvidence\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_RenewalAttempts_RenewalRequests_RequestId",
                        column: x => x.RequestId,
                        principalSchema: "Renewals",
                        principalTable: "RenewalRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RenewalOperations",
                schema: "Renewals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(32)", unicode: false, maxLength: 32, nullable: false),
                    RequestHash = table.Column<string>(type: "character varying(64)", unicode: false, maxLength: 64, nullable: false),
                    StatusAfter = table.Column<int>(type: "integer", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RenewalOperations", x => x.Id);
                    table.CheckConstraint("CK_RenewalOperation_Status", "\"StatusAfter\" IN (1,2,3,4,5)");
                    table.ForeignKey(
                        name: "FK_RenewalOperations_RenewalRequests_RequestId",
                        column: x => x.RequestId,
                        principalSchema: "Renewals",
                        principalTable: "RenewalRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RenewalAttempts_OperationId",
                schema: "Renewals",
                table: "RenewalAttempts",
                column: "OperationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RenewalAttempts_RequestId",
                schema: "Renewals",
                table: "RenewalAttempts",
                column: "RequestId",
                unique: true,
                filter: "\"Outcome\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_RenewalAttempts_RequestId_StartedAtUtc_Id",
                schema: "Renewals",
                table: "RenewalAttempts",
                columns: new[] { "RequestId", "StartedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_RenewalOperations_OperationId",
                schema: "Renewals",
                table: "RenewalOperations",
                column: "OperationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RenewalOperations_RequestId_OccurredAtUtc_Id",
                schema: "Renewals",
                table: "RenewalOperations",
                columns: new[] { "RequestId", "OccurredAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_RenewalRequests_OrderId",
                schema: "Renewals",
                table: "RenewalRequests",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_RenewalRequests_OrderItemId",
                schema: "Renewals",
                table: "RenewalRequests",
                column: "OrderItemId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RenewalRequests_OrderItemId_OrderId_VariantId_FulfillmentKi~",
                schema: "Renewals",
                table: "RenewalRequests",
                columns: new[] { "OrderItemId", "OrderId", "VariantId", "FulfillmentKind" });

            migrationBuilder.CreateIndex(
                name: "IX_RenewalRequests_Status_CreatedOn_Id",
                schema: "Renewals",
                table: "RenewalRequests",
                columns: new[] { "Status", "CreatedOn", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_RenewalRequests_TargetKey",
                schema: "Renewals",
                table: "RenewalRequests",
                column: "TargetKey",
                unique: true,
                filter: "\"Status\" IN (3,4) AND \"TargetKey\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reject before any DDL so a blocked older downgrade retains the latest applied schema.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "Renewals"."RenewalRequests")
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
                        RAISE EXCEPTION 'ManualRenewals cannot be rolled back while renewal/shipping/payment history or stock reservations exist. Preserve history with a forward migration or restore an earlier backup.';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropTable(
                name: "RenewalAttempts",
                schema: "Renewals");

            migrationBuilder.DropTable(
                name: "RenewalOperations",
                schema: "Renewals");

            migrationBuilder.DropTable(
                name: "RenewalRequests",
                schema: "Renewals");
        }
    }
}
