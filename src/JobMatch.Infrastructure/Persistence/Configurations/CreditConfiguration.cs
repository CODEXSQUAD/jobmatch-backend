using JobMatch.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobMatch.Infrastructure.Persistence.Configurations;

internal sealed class CreditAccountConfiguration
    : IEntityTypeConfiguration<CreditAccount>
{
    public void Configure(EntityTypeBuilder<CreditAccount> builder)
    {
        builder.ToTable("credit_account", table =>
            table.HasCheckConstraint(
                "ck_credit_account_balance",
                "daily_limit > 0 AND balance >= 0 AND balance <= daily_limit"));
        builder.HasKey(x => x.Id).HasName("pk_credit_account");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CandidateProfileId).HasColumnName("candidate_profile_id");
        builder.Property(x => x.PeriodDate).HasColumnName("period_date").HasColumnType("date");
        builder.Property(x => x.Balance).HasColumnName("balance");
        builder.Property(x => x.DailyLimit).HasColumnName("daily_limit");
        builder.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");
        builder.HasIndex(x => x.CandidateProfileId).IsUnique().HasDatabaseName("ux_credit_account_candidate_profile_id");
        builder.HasOne(x => x.CandidateProfile)
            .WithOne(x => x.CreditAccount)
            .HasForeignKey<CreditAccount>(x => x.CandidateProfileId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_credit_account_candidate_profile");
    }
}

internal sealed class CreditTransactionConfiguration
    : IEntityTypeConfiguration<CreditTransaction>
{
    public void Configure(EntityTypeBuilder<CreditTransaction> builder)
    {
        builder.ToTable("credit_transaction", table =>
        {
            table.HasCheckConstraint(
                "ck_credit_transaction_type",
                "type IN ('DailyReset', 'Debit')");
            table.HasCheckConstraint(
                "ck_credit_transaction_amount",
                "amount > 0");
            table.HasCheckConstraint(
                "ck_credit_transaction_application",
                "(type = 'Debit' AND application_id IS NOT NULL) OR " +
                "(type = 'DailyReset' AND application_id IS NULL)");
        });
        builder.HasKey(x => x.Id).HasName("pk_credit_transaction");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CreditAccountId).HasColumnName("credit_account_id");
        builder.Property(x => x.ApplicationId).HasColumnName("application_id");
        builder.Property(x => x.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Amount).HasColumnName("amount");
        builder.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(100).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
        builder.HasIndex(x => x.CreditAccountId).HasDatabaseName("ix_credit_transaction_credit_account_id");
        builder.HasIndex(x => x.ApplicationId)
            .IsUnique()
            .HasFilter("application_id IS NOT NULL AND type = 'Debit'")
            .HasDatabaseName("ux_credit_transaction_debit_application_id");
        builder.HasOne(x => x.CreditAccount)
            .WithMany(x => x.Transactions)
            .HasForeignKey(x => x.CreditAccountId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_credit_transaction_credit_account");
        builder.HasOne(x => x.Application)
            .WithMany()
            .HasForeignKey(x => x.ApplicationId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_credit_transaction_application");
    }
}
