using Boilerate.Domain.Ordering;
using Boilerate.Domain.Renewals;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Infrastructure.Persistence.Configuration;

internal static class RenewalModel
{
    internal static void Configure(ModelBuilder m, bool postgres)
    {
        var request = m.Entity<RenewalRequest>();
        request.HasBaseType((Type?)null);
        request.HasKey(x => x.Id);
        request.Property(x => x.Id).ValueGeneratedNever();
        request.Property(x => x.Version).HasMaxLength(32).IsConcurrencyToken();
        request.ToTable("RenewalRequests", "Renewals", t =>
        {
            t.HasCheckConstraint("CK_RenewalRequest_Kind", "\"FulfillmentKind\" = 4");
            t.HasCheckConstraint("CK_RenewalRequest_Status", "\"Status\" IN (1,2,3,4,5)");
            t.HasCheckConstraint("CK_RenewalRequest_Target", "(\"Status\" = 1 AND \"ProtectedTarget\" IS NULL AND \"TargetKey\" IS NULL AND \"TargetPreview\" IS NULL) OR (\"Status\" <> 1 AND \"ProtectedTarget\" IS NOT NULL AND \"TargetKey\" IS NOT NULL AND \"TargetPreview\" IS NOT NULL)");
            t.HasCheckConstraint("CK_RenewalRequest_TargetLock", postgres
                ? "(\"Status\" <> 1 OR NOT \"TargetLocked\") AND (\"Status\" NOT IN (4,5) OR \"TargetLocked\")"
                : "(\"Status\" <> 1 OR \"TargetLocked\" = 0) AND (\"Status\" NOT IN (4,5) OR \"TargetLocked\" = 1)");
            t.HasCheckConstraint("CK_RenewalRequest_Claim", "(\"Status\" = 3 AND \"ClaimedBy\" IS NOT NULL AND \"ClaimToken\" IS NOT NULL AND \"ClaimUntilUtc\" IS NOT NULL) OR (\"Status\" = 4 AND \"ClaimedBy\" IS NOT NULL AND \"ClaimToken\" IS NULL AND \"ClaimUntilUtc\" IS NULL) OR (\"Status\" IN (1,2,5) AND \"ClaimedBy\" IS NULL AND \"ClaimToken\" IS NULL AND \"ClaimUntilUtc\" IS NULL)");
            t.HasCheckConstraint("CK_RenewalRequest_Completion", "(\"Status\" = 5 AND \"CompletedAtUtc\" IS NOT NULL AND \"VerifiedExpiresAtUtc\" IS NOT NULL AND \"VerifiedExpiresAtUtc\" > \"CompletedAtUtc\") OR (\"Status\" <> 5 AND \"CompletedAtUtc\" IS NULL AND \"VerifiedExpiresAtUtc\" IS NULL)");
        });
        request.Property(x => x.ProtectedTarget).HasMaxLength(4000);
        request.Property(x => x.TargetKey).HasMaxLength(64).IsUnicode(false);
        request.Property(x => x.TargetPreview).HasMaxLength(100);
        request.HasIndex(x => x.OrderItemId).IsUnique();
        request.HasIndex(x => x.OrderId);
        request.HasIndex(x => new { x.Status, x.CreatedOn, x.Id });
        request.HasIndex(x => x.TargetKey).IsUnique().HasFilter(postgres
            ? "\"Status\" IN (3,4) AND \"TargetKey\" IS NOT NULL"
            : "[Status] IN (3,4) AND [TargetKey] IS NOT NULL");
        request.HasOne<OrderItem>().WithMany()
            .HasForeignKey(x => new { x.OrderItemId, x.OrderId, x.VariantId, x.FulfillmentKind })
            .HasPrincipalKey(x => new { x.Id, x.OrderId, x.VariantId, x.FulfillmentKind })
            .OnDelete(DeleteBehavior.Restrict);

        var attempt = m.Entity<RenewalAttempt>();
        attempt.HasBaseType((Type?)null);
        attempt.HasKey(x => x.Id);
        attempt.Property(x => x.Id).ValueGeneratedNever();
        attempt.ToTable("RenewalAttempts", "Renewals", t =>
        {
            t.HasCheckConstraint("CK_RenewalAttempt_Outcome", "\"Outcome\" IN (1,2,3)");
            t.HasCheckConstraint("CK_RenewalAttempt_Verification", "(\"Outcome\" = 1 AND \"VerifiedBy\" IS NULL AND \"VerifiedAtUtc\" IS NULL AND \"NewExpiresAtUtc\" IS NULL AND \"ProtectedEvidence\" IS NULL) OR (\"Outcome\" = 2 AND \"VerifiedBy\" IS NOT NULL AND \"VerifiedAtUtc\" IS NOT NULL AND \"VerifiedAtUtc\" >= \"StartedAtUtc\" AND \"NewExpiresAtUtc\" IS NOT NULL AND \"NewExpiresAtUtc\" > \"VerifiedAtUtc\" AND \"ProtectedEvidence\" IS NOT NULL) OR (\"Outcome\" = 3 AND \"VerifiedBy\" IS NOT NULL AND \"VerifiedAtUtc\" IS NOT NULL AND \"VerifiedAtUtc\" >= \"StartedAtUtc\" AND \"NewExpiresAtUtc\" IS NULL AND \"ProtectedEvidence\" IS NOT NULL)");
        });
        attempt.Property(x => x.ProtectedEvidence).HasMaxLength(32000);
        attempt.HasOne<RenewalRequest>().WithMany(x => x.Attempts)
            .HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Restrict);
        attempt.HasIndex(x => x.OperationId).IsUnique();
        attempt.HasIndex(x => x.RequestId).IsUnique()
            .HasFilter(postgres ? "\"Outcome\" = 1" : "[Outcome] = 1");
        attempt.HasIndex(x => new { x.RequestId, x.StartedAtUtc, x.Id });

        var operation = m.Entity<RenewalOperation>();
        operation.HasBaseType((Type?)null);
        operation.HasKey(x => x.Id);
        operation.Property(x => x.Id).ValueGeneratedNever();
        operation.ToTable("RenewalOperations", "Renewals", t =>
            t.HasCheckConstraint("CK_RenewalOperation_Status", "\"StatusAfter\" IN (1,2,3,4,5)"));
        operation.Property(x => x.Action).HasMaxLength(32).IsUnicode(false).IsRequired();
        operation.Property(x => x.RequestHash).HasMaxLength(64).IsUnicode(false).IsRequired();
        operation.HasOne<RenewalRequest>().WithMany().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Restrict);
        operation.HasIndex(x => x.OperationId).IsUnique();
        operation.HasIndex(x => new { x.RequestId, x.OccurredAtUtc, x.Id });
    }
}
