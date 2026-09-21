using JobMatch.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobMatch.Infrastructure.Persistence.Configurations;

internal sealed class ApplicationUserConfiguration
    : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.ToTable("users", table =>
            table.HasCheckConstraint(
                "ck_users_role",
                "role IN ('Candidate', 'Employer')"));

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.UserName).HasColumnName("user_name").HasMaxLength(320);
        builder.Property(x => x.NormalizedUserName).HasColumnName("normalized_user_name").HasMaxLength(320);
        builder.Property(x => x.Email).HasColumnName("email").HasColumnType("citext").HasMaxLength(320).IsRequired();
        builder.Property(x => x.NormalizedEmail).HasColumnName("normalized_email").HasMaxLength(320).IsRequired();
        builder.Property(x => x.EmailConfirmed).HasColumnName("email_confirmed");
        builder.Property(x => x.PasswordHash).HasColumnName("password_hash").HasMaxLength(500).IsRequired();
        builder.Property(x => x.SecurityStamp).HasColumnName("security_stamp");
        builder.Property(x => x.ConcurrencyStamp).HasColumnName("concurrency_stamp");
        builder.Property(x => x.PhoneNumber).HasColumnName("phone_number").HasMaxLength(32);
        builder.Property(x => x.PhoneNumberConfirmed).HasColumnName("phone_number_confirmed");
        builder.Property(x => x.TwoFactorEnabled).HasColumnName("two_factor_enabled");
        builder.Property(x => x.LockoutEnd).HasColumnName("lockout_end");
        builder.Property(x => x.LockoutEnabled).HasColumnName("lockout_enabled");
        builder.Property(x => x.AccessFailedCount).HasColumnName("access_failed_count");
        builder.Property(x => x.Role).HasColumnName("role").HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");

        builder.HasIndex(x => x.Email)
            .IsUnique()
            .HasDatabaseName("ux_users_email");

        builder.HasIndex(x => x.NormalizedUserName)
            .IsUnique()
            .HasDatabaseName("ux_users_normalized_user_name");

        builder.HasIndex(x => x.NormalizedEmail)
            .HasDatabaseName("ix_users_normalized_email");
    }
}

internal sealed class IdentityUserClaimConfiguration
    : IEntityTypeConfiguration<IdentityUserClaim<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserClaim<Guid>> builder)
    {
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.ClaimType).HasColumnName("claim_type");
        builder.Property(x => x.ClaimValue).HasColumnName("claim_value");
        builder.HasIndex(x => x.UserId).HasDatabaseName("ix_user_claim_user_id");
    }
}

internal sealed class IdentityUserLoginConfiguration
    : IEntityTypeConfiguration<IdentityUserLogin<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserLogin<Guid>> builder)
    {
        builder.Property(x => x.LoginProvider).HasColumnName("login_provider");
        builder.Property(x => x.ProviderKey).HasColumnName("provider_key");
        builder.Property(x => x.ProviderDisplayName).HasColumnName("provider_display_name");
        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.HasIndex(x => x.UserId).HasDatabaseName("ix_user_login_user_id");
    }
}

internal sealed class IdentityUserTokenConfiguration
    : IEntityTypeConfiguration<IdentityUserToken<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserToken<Guid>> builder)
    {
        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.LoginProvider).HasColumnName("login_provider");
        builder.Property(x => x.Name).HasColumnName("name");
        builder.Property(x => x.Value).HasColumnName("value");
    }
}
