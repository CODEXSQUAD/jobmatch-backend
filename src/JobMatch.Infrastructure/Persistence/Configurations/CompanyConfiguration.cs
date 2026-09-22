using JobMatch.Domain;
using JobMatch.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobMatch.Infrastructure.Persistence.Configurations;

internal sealed class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ToTable("company");
        builder.HasKey(x => x.Id).HasName("pk_company");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasColumnName("description").HasColumnType("text").IsRequired();
        builder.Property(x => x.City).HasColumnName("city").HasMaxLength(120).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");
    }
}

internal sealed class CompanyMemberConfiguration
    : IEntityTypeConfiguration<CompanyMember>
{
    public void Configure(EntityTypeBuilder<CompanyMember> builder)
    {
        builder.ToTable("company_member", table =>
            table.HasCheckConstraint(
                "ck_company_member_role",
                "member_role IN ('Owner')"));
        builder.HasKey(x => x.Id).HasName("pk_company_member");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CompanyId).HasColumnName("company_id");
        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.MemberRole).HasColumnName("member_role").HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
        builder.HasIndex(x => x.CompanyId).IsUnique().HasDatabaseName("ux_company_member_company_id");
        builder.HasIndex(x => x.UserId).IsUnique().HasDatabaseName("ux_company_member_user_id");
        builder.HasOne(x => x.Company)
            .WithOne(x => x.Member)
            .HasForeignKey<CompanyMember>(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_company_member_company");
        builder.HasOne<ApplicationUser>()
            .WithOne()
            .HasForeignKey<CompanyMember>(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_company_member_users");
    }
}
