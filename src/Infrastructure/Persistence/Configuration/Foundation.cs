using Boilerate.Domain.Identity;
using Boilerate.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Boilerate.Infrastructure.Persistence.Configuration;

public sealed class UserSessionConfig : IEntityTypeConfiguration<UserSession>
{
    public void Configure(EntityTypeBuilder<UserSession> builder)
    {
        builder.ToTable("UserSessions", SchemaNames.Identity);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RefreshTokenHash).HasMaxLength(64).IsRequired();
        builder.Property(x => x.UserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.IpAddress).HasMaxLength(64);
        builder.HasIndex(x => x.RefreshTokenHash).IsUnique();
        builder.HasIndex(x => new { x.UserId, x.FamilyId });
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class NotificationReceiptConfig : IEntityTypeConfiguration<NotificationReceipt>
{
    public void Configure(EntityTypeBuilder<NotificationReceipt> builder)
    {
        builder.ToTable("NotificationReceipts", SchemaNames.Notifications);
        builder.HasKey(x => new { x.NotificationId, x.UserId });
        builder.HasIndex(x => x.UserId);
        builder.HasOne(x => x.Notification).WithMany().HasForeignKey(x => x.NotificationId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AuthorizationLockConfig : IEntityTypeConfiguration<AuthorizationLock>
{
    public void Configure(EntityTypeBuilder<AuthorizationLock> builder)
    {
        builder.ToTable("AuthorizationLock", SchemaNames.Identity);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Version).HasMaxLength(32).IsRequired();
        builder.HasData(new AuthorizationLock { Id = 1, Version = "foundation" });
    }
}
