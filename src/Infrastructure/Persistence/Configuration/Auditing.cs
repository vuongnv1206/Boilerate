using Boilerate.Domain.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Boilerate.Infrastructure.Persistence.Configuration;

public class AuditTrailConfig : IEntityTypeConfiguration<Trail>
{
    public void Configure(EntityTypeBuilder<Trail> builder)
    {
        builder.ToTable("AuditTrails", "Auditing");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TableName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.PrimaryKey)
            .IsRequired()
            .HasMaxLength(500);

        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.TableName);
        builder.HasIndex(x => x.DateTime);
        builder.HasIndex(x => x.Type);
    }
}
