using JobMatch.Domain;
using JobMatch.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace JobMatch.Infrastructure.Persistence;

public class JobMatchDbContext(
    DbContextOptions<JobMatchDbContext> options)
    : IdentityUserContext<ApplicationUser, Guid>(options)
{
    public DbSet<CandidateProfile> CandidateProfiles => Set<CandidateProfile>();
    public DbSet<Resume> Resumes => Set<Resume>();
    public DbSet<WorkExperience> WorkExperiences => Set<WorkExperience>();
    public DbSet<Education> Educations => Set<Education>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<ResumeSkill> ResumeSkills => Set<ResumeSkill>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<CompanyMember> CompanyMembers => Set<CompanyMember>();
    public DbSet<Vacancy> Vacancies => Set<Vacancy>();
    public DbSet<VacancySkill> VacancySkills => Set<VacancySkill>();
    public DbSet<JobApplication> Applications => Set<JobApplication>();
    public DbSet<CreditAccount> CreditAccounts => Set<CreditAccount>();
    public DbSet<CreditTransaction> CreditTransactions => Set<CreditTransaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasPostgresExtension("citext");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(JobMatchDbContext).Assembly);

        modelBuilder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claim");
        modelBuilder.Entity<IdentityUserLogin<Guid>>().ToTable("user_login");
        modelBuilder.Entity<IdentityUserToken<Guid>>().ToTable("user_token");
    }
}
