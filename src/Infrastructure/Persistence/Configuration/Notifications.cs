using Boilerate.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Boilerate.Infrastructure.Persistence.Configuration;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications", SchemaNames.Notifications);

        builder.HasKey(n => n.Id);

        builder.Property(n => n.UserId)
            .IsRequired(false);

        builder.Property(n => n.TargetRole)
            .HasMaxLength(100)
            .IsRequired(false);

        builder.Property(n => n.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(n => n.Message)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(n => n.Type)
            .IsRequired();

        builder.Property(n => n.ReferenceType)
            .HasMaxLength(100)
            .IsRequired(false);

        builder.Property(n => n.ActionUrl)
            .HasMaxLength(500)
            .IsRequired(false);

        builder.Property(n => n.IsSent)
            .IsRequired()
            .HasDefaultValue(false);

        // Indexes
        builder.HasIndex(n => n.UserId);
        builder.HasIndex(n => n.TargetRole);
        builder.HasIndex(n => n.CreatedOn);
    }
}
