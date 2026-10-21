using Boilerate.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Boilerate.Infrastructure.Persistence.Configuration;

public class ApplicationUserConfig : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.ToTable("Users", SchemaNames.Identity);
        builder.Property(u => u.ObjectId).HasMaxLength(256);
    }
}

public class ApplicationRoleConfig : IEntityTypeConfiguration<ApplicationRole>
{
    public void Configure(EntityTypeBuilder<ApplicationRole> builder)
    {
        builder.ToTable("Roles", SchemaNames.Identity);
    }
}

public class ApplicationRoleClaimConfig : IEntityTypeConfiguration<ApplicationRoleClaim>
{
    public void Configure(EntityTypeBuilder<ApplicationRoleClaim> builder)
    {
        builder.ToTable("RoleClaims", SchemaNames.Identity);
    }
}

public class IdentityUserRoleConfig : IEntityTypeConfiguration<IdentityUserRole<string>>
{
    public void Configure(EntityTypeBuilder<IdentityUserRole<string>> builder)
    {
        builder.ToTable("UserRoles", SchemaNames.Identity);
    }
}

public class IdentityUserClaimConfig : IEntityTypeConfiguration<IdentityUserClaim<string>>
{
    public void Configure(EntityTypeBuilder<IdentityUserClaim<string>> builder)
    {
        builder.ToTable("UserClaims", SchemaNames.Identity);
    }
}

public class IdentityUserLoginConfig : IEntityTypeConfiguration<IdentityUserLogin<string>>
{
    public void Configure(EntityTypeBuilder<IdentityUserLogin<string>> builder)
    {
        builder.ToTable("UserLogins", SchemaNames.Identity);
    }
}

public class IdentityUserTokenConfig : IEntityTypeConfiguration<IdentityUserToken<string>>
{
    public void Configure(EntityTypeBuilder<IdentityUserToken<string>> builder)
    {
        builder.ToTable("UserTokens", SchemaNames.Identity);
    }
}

public class ActionConfig : IEntityTypeConfiguration<Domain.Identity.Action>
{
    public void Configure(EntityTypeBuilder<Domain.Identity.Action> builder)
    {
        builder.ToTable("Actions", SchemaNames.Identity);
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Name).IsRequired().HasMaxLength(100);
        builder.Property(a => a.Code).HasMaxLength(100).IsRequired();
        builder.Property(a => a.NormalizedCode).HasMaxLength(100).IsRequired();
        builder.HasIndex(a => a.NormalizedCode).IsUnique();
        builder.Property(a => a.Version).HasMaxLength(32).IsConcurrencyToken();
    }
}

public class FunctionConfig : IEntityTypeConfiguration<Function>
{
    public void Configure(EntityTypeBuilder<Function> builder)
    {
        builder.ToTable("Functions", SchemaNames.Identity);
        builder.HasKey(f => f.Id);
        builder.HasOne(f => f.Group).WithMany().HasForeignKey(f => f.GroupId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(f => f.Name).IsRequired().HasMaxLength(100);
        builder.Property(f => f.Code).HasMaxLength(100).IsRequired();
        builder.Property(f => f.NormalizedCode).HasMaxLength(100).IsRequired();
        builder.HasIndex(f => f.NormalizedCode).IsUnique();
        builder.Property(f => f.Version).HasMaxLength(32).IsConcurrencyToken();
    }
}

public sealed class FunctionGroupConfig : IEntityTypeConfiguration<FunctionGroup>
{
    public void Configure(EntityTypeBuilder<FunctionGroup> builder)
    {
        builder.ToTable("FunctionGroups", SchemaNames.Identity, table => table.HasCheckConstraint("CK_FunctionGroups_SortOrder", "\"SortOrder\" >= 0"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(100).IsRequired();
        builder.Property(x => x.NormalizedCode).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Version).HasMaxLength(32).IsRequired().IsConcurrencyToken();
        builder.HasIndex(x => x.NormalizedCode).IsUnique();
        builder.HasData(FunctionGroup.InitialGroups());
    }
}

public class ActionInFunctionConfig : IEntityTypeConfiguration<ActionInFunction>
{
    public void Configure(EntityTypeBuilder<ActionInFunction> builder)
    {
        builder.ToTable("ActionInFunctions", SchemaNames.Identity);

        // Composite key: mỗi cặp (Action, Function) chỉ xuất hiện duy nhất 1 lần
        builder.HasKey(aif => new { aif.ActionId, aif.FunctionId });

        builder.HasOne(aif => aif.Action)
            .WithMany(a => a.ActionInFunctions)
            .HasForeignKey(aif => aif.ActionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(aif => aif.Function)
            .WithMany(f => f.ActionInFunctions)
            .HasForeignKey(aif => aif.FunctionId)
            .OnDelete(DeleteBehavior.Restrict);

    }
}

public class PermissionConfig : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("Permissions", SchemaNames.Identity);
        builder.HasKey(p => p.Id);

        // Tránh duplicate: một Role chỉ có 1 permission cho 1 cặp (Function, Action)
        builder.HasIndex(p => new { p.RoleId, p.FunctionId, p.ActionId }).IsUnique();

        builder.HasOne<ActionInFunction>().WithMany()
            .HasForeignKey(p => new { p.ActionId, p.FunctionId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Role)
            .WithMany()
            .HasForeignKey(p => p.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.Function)
            .WithMany()
            .HasForeignKey(p => p.FunctionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Action)
            .WithMany()
            .HasForeignKey(p => p.ActionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
