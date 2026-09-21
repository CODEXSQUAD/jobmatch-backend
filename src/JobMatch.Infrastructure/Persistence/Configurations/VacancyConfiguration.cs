using JobMatch.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobMatch.Infrastructure.Persistence.Configurations;

internal sealed class VacancyConfiguration : IEntityTypeConfiguration<Vacancy>
{
    public void Configure(EntityTypeBuilder<Vacancy> builder)
    {
        builder.ToTable("vacancy", table =>
        {
            table.HasCheckConstraint(
                "ck_vacancy_work_format",
                "work_format IN ('Office', 'Remote', 'Hybrid')");
            table.HasCheckConstraint(
                "ck_vacancy_status",
                "status IN ('Draft', 'Published', 'Closed')");
            table.HasCheckConstraint(
                "ck_vacancy_salary",
                "(salary_from IS NULL OR salary_from >= 0) AND " +
                "(salary_to IS NULL OR salary_to >= 0) AND " +
                "(salary_from IS NULL OR salary_to IS NULL OR salary_from <= salary_to)");
        });
        builder.HasKey(x => x.Id).HasName("pk_vacancy");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CompanyId).HasColumnName("company_id");
        builder.Property(x => x.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasColumnName("description").HasColumnType("text").IsRequired();
        builder.Property(x => x.Requirements).HasColumnName("requirements").HasColumnType("text").IsRequired();
        builder.Property(x => x.City).HasColumnName("city").HasMaxLength(120).IsRequired();
        builder.Property(x => x.WorkFormat).HasColumnName("work_format").HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.SalaryFrom).HasColumnName("salary_from");
        builder.Property(x => x.SalaryTo).HasColumnName("salary_to");
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.PublishedAt).HasColumnName("published_at").HasColumnType("timestamp with time zone");
        builder.Property(x => x.ClosedAt).HasColumnName("closed_at").HasColumnType("timestamp with time zone");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");
        builder.HasIndex(x => x.CompanyId).HasDatabaseName("ix_vacancy_company_id");
        builder.HasIndex(x => new { x.Status, x.PublishedAt })
            .IsDescending(false, true)
            .HasDatabaseName("ix_vacancy_status_published_at");
        builder.HasIndex(x => x.City).HasDatabaseName("ix_vacancy_city");
        builder.HasIndex(x => x.WorkFormat).HasDatabaseName("ix_vacancy_work_format");
        builder.HasOne(x => x.Company)
            .WithMany(x => x.Vacancies)
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_vacancy_company");
    }
}

internal sealed class VacancySkillConfiguration
    : IEntityTypeConfiguration<VacancySkill>
{
    public void Configure(EntityTypeBuilder<VacancySkill> builder)
    {
        builder.ToTable("vacancy_skill");
        builder.HasKey(x => new { x.VacancyId, x.SkillId }).HasName("pk_vacancy_skill");
        builder.Property(x => x.VacancyId).HasColumnName("vacancy_id");
        builder.Property(x => x.SkillId).HasColumnName("skill_id");
        builder.HasIndex(x => x.SkillId).HasDatabaseName("ix_vacancy_skill_skill_id");
        builder.HasOne(x => x.Vacancy)
            .WithMany(x => x.VacancySkills)
            .HasForeignKey(x => x.VacancyId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_vacancy_skill_vacancy");
        builder.HasOne(x => x.Skill)
            .WithMany(x => x.VacancySkills)
            .HasForeignKey(x => x.SkillId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_vacancy_skill_skill");
    }
}

internal sealed class JobApplicationConfiguration
    : IEntityTypeConfiguration<JobApplication>
{
    public void Configure(EntityTypeBuilder<JobApplication> builder)
    {
        builder.ToTable("application", table =>
        {
            table.HasCheckConstraint(
                "ck_application_status",
                "status IN ('Submitted', 'Viewed', 'Invited', 'Rejected', 'Withdrawn')");
            table.HasCheckConstraint(
                "ck_application_snapshot_version",
                "snapshot_version > 0");
        });
        builder.HasKey(x => x.Id).HasName("pk_application");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CandidateProfileId).HasColumnName("candidate_profile_id");
        builder.Property(x => x.ResumeId).HasColumnName("resume_id");
        builder.Property(x => x.VacancyId).HasColumnName("vacancy_id");
        builder.Property(x => x.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(100).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.CoverLetter).HasColumnName("cover_letter").HasColumnType("text");
        builder.Property(x => x.ResumeSnapshot).HasColumnName("resume_snapshot").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.SnapshotVersion).HasColumnName("snapshot_version").HasDefaultValue(1);
        builder.Property(x => x.SubmittedAt).HasColumnName("submitted_at").HasColumnType("timestamp with time zone");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");
        builder.HasIndex(x => new { x.CandidateProfileId, x.VacancyId })
            .IsUnique()
            .HasDatabaseName("ux_application_candidate_vacancy");
        builder.HasIndex(x => new { x.CandidateProfileId, x.IdempotencyKey })
            .IsUnique()
            .HasDatabaseName("ux_application_candidate_idempotency");
        builder.HasIndex(x => x.ResumeId).HasDatabaseName("ix_application_resume_id");
        builder.HasIndex(x => x.VacancyId).HasDatabaseName("ix_application_vacancy_id");
        builder.HasOne(x => x.CandidateProfile)
            .WithMany(x => x.Applications)
            .HasForeignKey(x => x.CandidateProfileId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_application_candidate_profile");
        builder.HasOne(x => x.Resume)
            .WithMany(x => x.Applications)
            .HasForeignKey(x => x.ResumeId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_application_resume");
        builder.HasOne(x => x.Vacancy)
            .WithMany(x => x.Applications)
            .HasForeignKey(x => x.VacancyId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_application_vacancy");
    }
}
