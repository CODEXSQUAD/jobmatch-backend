using System.Text.Json;

namespace JobMatch.Domain;

public sealed class CandidateProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? City { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Resume? Resume { get; set; }
    public CreditAccount? CreditAccount { get; set; }
    public ICollection<JobApplication> Applications { get; set; } = [];
}

public sealed class Resume
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CandidateProfileId { get; set; }
    public string DesiredPosition { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public CandidateProfile CandidateProfile { get; set; } = null!;
    public ICollection<WorkExperience> WorkExperiences { get; set; } = [];
    public ICollection<Education> Educations { get; set; } = [];
    public ICollection<ResumeSkill> ResumeSkills { get; set; } = [];
    public ICollection<JobApplication> Applications { get; set; } = [];
}

public sealed class WorkExperience
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ResumeId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public DateOnly StartedOn { get; set; }
    public DateOnly? EndedOn { get; set; }
    public string? Description { get; set; }
    public Resume Resume { get; set; } = null!;
}

public sealed class Education
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ResumeId { get; set; }
    public string Institution { get; set; } = string.Empty;
    public string Specialty { get; set; } = string.Empty;
    public string? Degree { get; set; }
    public short? GraduationYear { get; set; }
    public Resume Resume { get; set; } = null!;
}

public sealed class Skill
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public ICollection<ResumeSkill> ResumeSkills { get; set; } = [];
    public ICollection<VacancySkill> VacancySkills { get; set; } = [];
}

public sealed class ResumeSkill
{
    public Guid ResumeId { get; set; }
    public Guid SkillId { get; set; }
    public Resume Resume { get; set; } = null!;
    public Skill Skill { get; set; } = null!;
}

public sealed class Company
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public CompanyMember? Member { get; set; }
    public ICollection<Vacancy> Vacancies { get; set; } = [];
}

public sealed class CompanyMember
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CompanyId { get; set; }
    public Guid UserId { get; set; }
    public CompanyMemberRole MemberRole { get; set; } = CompanyMemberRole.Owner;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Company Company { get; set; } = null!;
}

public sealed class Vacancy
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CompanyId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Requirements { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public WorkFormat WorkFormat { get; set; }
    public int? SalaryFrom { get; set; }
    public int? SalaryTo { get; set; }
    public VacancyStatus Status { get; set; } = VacancyStatus.Draft;
    public DateTimeOffset? PublishedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Company Company { get; set; } = null!;
    public ICollection<VacancySkill> VacancySkills { get; set; } = [];
    public ICollection<JobApplication> Applications { get; set; } = [];
}

public sealed class VacancySkill
{
    public Guid VacancyId { get; set; }
    public Guid SkillId { get; set; }
    public Vacancy Vacancy { get; set; } = null!;
    public Skill Skill { get; set; } = null!;
}

public sealed class JobApplication
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CandidateProfileId { get; set; }
    public Guid ResumeId { get; set; }
    public Guid VacancyId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public ApplicationStatus Status { get; set; } = ApplicationStatus.Submitted;
    public string? CoverLetter { get; set; }
    public JsonDocument ResumeSnapshot { get; set; } = null!;
    public int SnapshotVersion { get; set; } = 1;
    public DateTimeOffset SubmittedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public CandidateProfile CandidateProfile { get; set; } = null!;
    public Resume Resume { get; set; } = null!;
    public Vacancy Vacancy { get; set; } = null!;
}

public sealed class CreditAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CandidateProfileId { get; set; }
    public DateOnly PeriodDate { get; set; }
    public int Balance { get; set; }
    public int DailyLimit { get; set; }
    public int Version { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public CandidateProfile CandidateProfile { get; set; } = null!;
    public ICollection<CreditTransaction> Transactions { get; set; } = [];
}

public sealed class CreditTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CreditAccountId { get; set; }
    public Guid? ApplicationId { get; set; }
    public CreditTransactionType Type { get; set; }
    public int Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public CreditAccount CreditAccount { get; set; } = null!;
    public JobApplication? Application { get; set; }
}
