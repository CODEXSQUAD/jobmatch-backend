using JobMatch.Domain;
using JobMatch.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobMatch.Infrastructure.Persistence.Configurations;

internal sealed class CandidateProfileConfiguration
    : IEntityTypeConfiguration<CandidateProfile>
{
    public void Configure(EntityTypeBuilder<CandidateProfile> builder)
    {
        builder.ToTable("candidate_profile");
        builder.HasKey(x => x.Id).HasName("pk_candidate_profile");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.FullName).HasColumnName("full_name").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Phone).HasColumnName("phone").HasMaxLength(32);
        builder.Property(x => x.City).HasColumnName("city").HasMaxLength(120);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");
        builder.HasIndex(x => x.UserId).IsUnique().HasDatabaseName("ux_candidate_profile_user_id");
        builder.HasOne<ApplicationUser>()
            .WithOne()
            .HasForeignKey<CandidateProfile>(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_candidate_profile_users");
    }
}

internal sealed class ResumeConfiguration : IEntityTypeConfiguration<Resume>
{
    public void Configure(EntityTypeBuilder<Resume> builder)
    {
        builder.ToTable("resume");
        builder.HasKey(x => x.Id).HasName("pk_resume");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CandidateProfileId).HasColumnName("candidate_profile_id");
        builder.Property(x => x.DesiredPosition).HasColumnName("desired_position").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Summary).HasColumnName("summary").HasColumnType("text").IsRequired();
        builder.Property(x => x.ContactEmail).HasColumnName("contact_email").HasMaxLength(320).IsRequired();
        builder.Property(x => x.ContactPhone).HasColumnName("contact_phone").HasMaxLength(32).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");
        builder.HasIndex(x => x.CandidateProfileId).IsUnique().HasDatabaseName("ux_resume_candidate_profile_id");
        builder.HasOne(x => x.CandidateProfile)
            .WithOne(x => x.Resume)
            .HasForeignKey<Resume>(x => x.CandidateProfileId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_resume_candidate_profile");
    }
}

internal sealed class WorkExperienceConfiguration
    : IEntityTypeConfiguration<WorkExperience>
{
    public void Configure(EntityTypeBuilder<WorkExperience> builder)
    {
        builder.ToTable("work_experience", table =>
            table.HasCheckConstraint(
                "ck_work_experience_dates",
                "ended_on IS NULL OR ended_on >= started_on"));
        builder.HasKey(x => x.Id).HasName("pk_work_experience");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ResumeId).HasColumnName("resume_id");
        builder.Property(x => x.CompanyName).HasColumnName("company_name").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Position).HasColumnName("position").HasMaxLength(200).IsRequired();
        builder.Property(x => x.StartedOn).HasColumnName("started_on").HasColumnType("date");
        builder.Property(x => x.EndedOn).HasColumnName("ended_on").HasColumnType("date");
        builder.Property(x => x.Description).HasColumnName("description").HasColumnType("text");
        builder.HasIndex(x => x.ResumeId).HasDatabaseName("ix_work_experience_resume_id");
        builder.HasOne(x => x.Resume)
            .WithMany(x => x.WorkExperiences)
            .HasForeignKey(x => x.ResumeId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_work_experience_resume");
    }
}

internal sealed class EducationConfiguration : IEntityTypeConfiguration<Education>
{
    public void Configure(EntityTypeBuilder<Education> builder)
    {
        builder.ToTable("education", table =>
            table.HasCheckConstraint(
                "ck_education_graduation_year",
                "graduation_year IS NULL OR graduation_year BETWEEN 1900 AND 2100"));
        builder.HasKey(x => x.Id).HasName("pk_education");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ResumeId).HasColumnName("resume_id");
        builder.Property(x => x.Institution).HasColumnName("institution").HasMaxLength(250).IsRequired();
        builder.Property(x => x.Specialty).HasColumnName("specialty").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Degree).HasColumnName("degree").HasMaxLength(100);
        builder.Property(x => x.GraduationYear).HasColumnName("graduation_year");
        builder.HasIndex(x => x.ResumeId).HasDatabaseName("ix_education_resume_id");
        builder.HasOne(x => x.Resume)
            .WithMany(x => x.Educations)
            .HasForeignKey(x => x.ResumeId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_education_resume");
    }
}

internal sealed class SkillConfiguration : IEntityTypeConfiguration<Skill>
{
    public void Configure(EntityTypeBuilder<Skill> builder)
    {
        builder.ToTable("skill");
        builder.HasKey(x => x.Id).HasName("pk_skill");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Name).HasColumnName("name").HasColumnType("citext").HasMaxLength(100).IsRequired();
        builder.HasIndex(x => x.Name).IsUnique().HasDatabaseName("ux_skill_name");
    }
}

internal sealed class ResumeSkillConfiguration
    : IEntityTypeConfiguration<ResumeSkill>
{
    public void Configure(EntityTypeBuilder<ResumeSkill> builder)
    {
        builder.ToTable("resume_skill");
        builder.HasKey(x => new { x.ResumeId, x.SkillId }).HasName("pk_resume_skill");
        builder.Property(x => x.ResumeId).HasColumnName("resume_id");
        builder.Property(x => x.SkillId).HasColumnName("skill_id");
        builder.HasIndex(x => x.SkillId).HasDatabaseName("ix_resume_skill_skill_id");
        builder.HasOne(x => x.Resume)
            .WithMany(x => x.ResumeSkills)
            .HasForeignKey(x => x.ResumeId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_resume_skill_resume");
        builder.HasOne(x => x.Skill)
            .WithMany(x => x.ResumeSkills)
            .HasForeignKey(x => x.SkillId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_resume_skill_skill");
    }
}
