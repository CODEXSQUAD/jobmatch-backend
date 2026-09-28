using JobMatch.Domain;
using JobMatch.Infrastructure.Identity;
using JobMatch.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace JobMatch.Infrastructure.Seeding;

public static class DemoDataSeeder
{
    private static readonly DateTimeOffset SeededAt =
        new(2026, 9, 20, 9, 0, 0, TimeSpan.Zero);

    public static async Task SeedAsync(
        JobMatchDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        var users = CreateUsers();
        var companies = CreateCompanies();
        var members = CreateCompanyMembers();
        var vacancies = CreateVacancies();
        var skills = CreateSkills();
        var vacancySkills = CreateVacancySkills();

        await AddMissingAsync(
            dbContext.Users,
            users,
            user => user.Id,
            cancellationToken);
        await AddMissingAsync(
            dbContext.Companies,
            companies,
            company => company.Id,
            cancellationToken);
        await AddMissingAsync(
            dbContext.Skills,
            skills,
            skill => skill.Id,
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        await AddMissingAsync(
            dbContext.CompanyMembers,
            members,
            member => member.Id,
            cancellationToken);
        await AddMissingAsync(
            dbContext.Vacancies,
            vacancies,
            vacancy => vacancy.Id,
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (var vacancySkill in vacancySkills)
        {
            var exists = await dbContext.VacancySkills.AnyAsync(
                item => item.VacancyId == vacancySkill.VacancyId &&
                        item.SkillId == vacancySkill.SkillId,
                cancellationToken);

            if (!exists)
            {
                await dbContext.VacancySkills.AddAsync(
                    vacancySkill,
                    cancellationToken);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task AddMissingAsync<TEntity>(
        DbSet<TEntity> set,
        IEnumerable<TEntity> entities,
        Func<TEntity, Guid> idSelector,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        foreach (var entity in entities)
        {
            var id = idSelector(entity);

            if (!await set.AnyAsync(item => EF.Property<Guid>(item, "Id") == id, cancellationToken))
            {
                await set.AddAsync(entity, cancellationToken);
            }
        }
    }

    private static ApplicationUser[] CreateUsers()
    {
        var users = new[]
        {
            CreateEmployer(
                DemoIds.NorthernCodeUser,
                "employer.northern-code@example.test"),
            CreateEmployer(
                DemoIds.VolgaAnalyticsUser,
                "employer.volga-analytics@example.test"),
            CreateEmployer(
                DemoIds.UralRoboticsUser,
                "employer.ural-robotics@example.test")
        };

        var passwordHasher = new PasswordHasher<ApplicationUser>();

        foreach (var user in users)
        {
            user.PasswordHash = passwordHasher.HashPassword(
                user,
                "EmployerDemo123!");
        }

        return users;
    }

    private static ApplicationUser CreateEmployer(Guid id, string email)
    {
        var normalizedEmail = email.ToUpperInvariant();

        return new ApplicationUser
        {
            Id = id,
            UserName = email,
            NormalizedUserName = normalizedEmail,
            Email = email,
            NormalizedEmail = normalizedEmail,
            EmailConfirmed = true,
            Role = UserRole.Employer,
            IsActive = true,
            CreatedAt = SeededAt,
            UpdatedAt = SeededAt
        };
    }

    private static Company[] CreateCompanies() =>
    [
        new()
        {
            Id = DemoIds.NorthernCodeCompany,
            Name = "Северный Код",
            Description = "Разрабатывает цифровые сервисы для городской инфраструктуры.",
            City = "Санкт-Петербург",
            CreatedAt = SeededAt,
            UpdatedAt = SeededAt
        },
        new()
        {
            Id = DemoIds.VolgaAnalyticsCompany,
            Name = "Волга Аналитика",
            Description = "Создаёт аналитические продукты для розничного бизнеса.",
            City = "Казань",
            CreatedAt = SeededAt,
            UpdatedAt = SeededAt
        },
        new()
        {
            Id = DemoIds.UralRoboticsCompany,
            Name = "Урал Роботикс",
            Description = "Автоматизирует производственные и складские процессы.",
            City = "Екатеринбург",
            CreatedAt = SeededAt,
            UpdatedAt = SeededAt
        }
    ];

    private static CompanyMember[] CreateCompanyMembers() =>
    [
        CreateMember(DemoIds.NorthernCodeMember, DemoIds.NorthernCodeCompany, DemoIds.NorthernCodeUser),
        CreateMember(DemoIds.VolgaAnalyticsMember, DemoIds.VolgaAnalyticsCompany, DemoIds.VolgaAnalyticsUser),
        CreateMember(DemoIds.UralRoboticsMember, DemoIds.UralRoboticsCompany, DemoIds.UralRoboticsUser)
    ];

    private static CompanyMember CreateMember(Guid id, Guid companyId, Guid userId) =>
        new()
        {
            Id = id,
            CompanyId = companyId,
            UserId = userId,
            MemberRole = CompanyMemberRole.Owner,
            CreatedAt = SeededAt
        };

    private static Vacancy[] CreateVacancies() =>
    [
        PublishedVacancy(
            DemoIds.BackendVacancy,
            DemoIds.NorthernCodeCompany,
            "Backend-разработчик C#",
            "Разработка API и интеграций для городских сервисов.",
            "C#, ASP.NET Core, PostgreSQL и опыт создания REST API.",
            "Санкт-Петербург",
            WorkFormat.Hybrid,
            160_000,
            230_000,
            SeededAt.AddDays(6)),
        PublishedVacancy(
            DemoIds.FrontendVacancy,
            DemoIds.NorthernCodeCompany,
            "Frontend-разработчик React",
            "Развитие личных кабинетов и дизайн-системы.",
            "TypeScript, React и понимание доступности интерфейсов.",
            "Москва",
            WorkFormat.Remote,
            140_000,
            210_000,
            SeededAt.AddDays(5)),
        PublishedVacancy(
            DemoIds.AnalystVacancy,
            DemoIds.VolgaAnalyticsCompany,
            "Системный аналитик",
            "Проектирование контрактов и моделей данных аналитической платформы.",
            "UML, SQL, REST и навыки описания требований.",
            "Казань",
            WorkFormat.Office,
            120_000,
            180_000,
            SeededAt.AddDays(4)),
        PublishedVacancy(
            DemoIds.DataEngineerVacancy,
            DemoIds.VolgaAnalyticsCompany,
            "Data Engineer",
            "Построение витрин данных и потоков загрузки.",
            "Python, PostgreSQL и практический опыт ETL.",
            "Москва",
            WorkFormat.Hybrid,
            180_000,
            260_000,
            SeededAt.AddDays(3)),
        PublishedVacancy(
            DemoIds.QaVacancy,
            DemoIds.UralRoboticsCompany,
            "QA-инженер",
            "Тестирование сервисов управления складской робототехникой.",
            "API-тестирование, SQL и опыт автоматизации проверок.",
            "Екатеринбург",
            WorkFormat.Office,
            110_000,
            170_000,
            SeededAt.AddDays(2)),
        PublishedVacancy(
            DemoIds.DevOpsVacancy,
            DemoIds.UralRoboticsCompany,
            "DevOps-инженер",
            "Поддержка среды разработки и поставки сервисов.",
            "Linux, Docker, CI/CD и основы PostgreSQL.",
            "Новосибирск",
            WorkFormat.Remote,
            null,
            250_000,
            SeededAt.AddDays(1)),
        new()
        {
            Id = DemoIds.DraftVacancy,
            CompanyId = DemoIds.NorthernCodeCompany,
            Title = "Архитектор решений (черновик)",
            Description = "Черновик для проверки исключения из каталога.",
            Requirements = "Требования уточняются.",
            City = "Москва",
            WorkFormat = WorkFormat.Hybrid,
            Status = VacancyStatus.Draft,
            CreatedAt = SeededAt.AddDays(7),
            UpdatedAt = SeededAt.AddDays(7)
        },
        new()
        {
            Id = DemoIds.ClosedVacancy,
            CompanyId = DemoIds.VolgaAnalyticsCompany,
            Title = "Product Manager (закрыта)",
            Description = "Закрытая вакансия для проверки активного каталога.",
            Requirements = "Опыт управления цифровым продуктом.",
            City = "Казань",
            WorkFormat = WorkFormat.Hybrid,
            SalaryFrom = 150_000,
            SalaryTo = 220_000,
            Status = VacancyStatus.Closed,
            PublishedAt = SeededAt.AddDays(-1),
            ClosedAt = SeededAt.AddDays(7),
            CreatedAt = SeededAt.AddDays(-2),
            UpdatedAt = SeededAt.AddDays(7)
        }
    ];

    private static Vacancy PublishedVacancy(
        Guid id,
        Guid companyId,
        string title,
        string description,
        string requirements,
        string city,
        WorkFormat workFormat,
        int? salaryFrom,
        int? salaryTo,
        DateTimeOffset publishedAt) =>
        new()
        {
            Id = id,
            CompanyId = companyId,
            Title = title,
            Description = description,
            Requirements = requirements,
            City = city,
            WorkFormat = workFormat,
            SalaryFrom = salaryFrom,
            SalaryTo = salaryTo,
            Status = VacancyStatus.Published,
            PublishedAt = publishedAt,
            CreatedAt = publishedAt.AddDays(-1),
            UpdatedAt = publishedAt
        };

    private static Skill[] CreateSkills() =>
    [
        new() { Id = DemoIds.CSharpSkill, Name = "C#" },
        new() { Id = DemoIds.PostgreSqlSkill, Name = "PostgreSQL" },
        new() { Id = DemoIds.ReactSkill, Name = "React" },
        new() { Id = DemoIds.TypeScriptSkill, Name = "TypeScript" },
        new() { Id = DemoIds.DockerSkill, Name = "Docker" },
        new() { Id = DemoIds.SqlSkill, Name = "SQL" }
    ];

    private static VacancySkill[] CreateVacancySkills() =>
    [
        new() { VacancyId = DemoIds.BackendVacancy, SkillId = DemoIds.CSharpSkill },
        new() { VacancyId = DemoIds.BackendVacancy, SkillId = DemoIds.PostgreSqlSkill },
        new() { VacancyId = DemoIds.FrontendVacancy, SkillId = DemoIds.ReactSkill },
        new() { VacancyId = DemoIds.FrontendVacancy, SkillId = DemoIds.TypeScriptSkill },
        new() { VacancyId = DemoIds.AnalystVacancy, SkillId = DemoIds.SqlSkill },
        new() { VacancyId = DemoIds.DataEngineerVacancy, SkillId = DemoIds.PostgreSqlSkill },
        new() { VacancyId = DemoIds.DataEngineerVacancy, SkillId = DemoIds.SqlSkill },
        new() { VacancyId = DemoIds.QaVacancy, SkillId = DemoIds.SqlSkill },
        new() { VacancyId = DemoIds.DevOpsVacancy, SkillId = DemoIds.DockerSkill },
        new() { VacancyId = DemoIds.DevOpsVacancy, SkillId = DemoIds.PostgreSqlSkill }
    ];

    private static class DemoIds
    {
        public static readonly Guid NorthernCodeUser = new("10000000-0000-4000-8000-000000000001");
        public static readonly Guid VolgaAnalyticsUser = new("10000000-0000-4000-8000-000000000002");
        public static readonly Guid UralRoboticsUser = new("10000000-0000-4000-8000-000000000003");

        public static readonly Guid NorthernCodeCompany = new("20000000-0000-4000-8000-000000000001");
        public static readonly Guid VolgaAnalyticsCompany = new("20000000-0000-4000-8000-000000000002");
        public static readonly Guid UralRoboticsCompany = new("20000000-0000-4000-8000-000000000003");

        public static readonly Guid NorthernCodeMember = new("30000000-0000-4000-8000-000000000001");
        public static readonly Guid VolgaAnalyticsMember = new("30000000-0000-4000-8000-000000000002");
        public static readonly Guid UralRoboticsMember = new("30000000-0000-4000-8000-000000000003");

        public static readonly Guid BackendVacancy = new("40000000-0000-4000-8000-000000000001");
        public static readonly Guid FrontendVacancy = new("40000000-0000-4000-8000-000000000002");
        public static readonly Guid AnalystVacancy = new("40000000-0000-4000-8000-000000000003");
        public static readonly Guid DataEngineerVacancy = new("40000000-0000-4000-8000-000000000004");
        public static readonly Guid QaVacancy = new("40000000-0000-4000-8000-000000000005");
        public static readonly Guid DevOpsVacancy = new("40000000-0000-4000-8000-000000000006");
        public static readonly Guid DraftVacancy = new("40000000-0000-4000-8000-000000000007");
        public static readonly Guid ClosedVacancy = new("40000000-0000-4000-8000-000000000008");

        public static readonly Guid CSharpSkill = new("50000000-0000-4000-8000-000000000001");
        public static readonly Guid PostgreSqlSkill = new("50000000-0000-4000-8000-000000000002");
        public static readonly Guid ReactSkill = new("50000000-0000-4000-8000-000000000003");
        public static readonly Guid TypeScriptSkill = new("50000000-0000-4000-8000-000000000004");
        public static readonly Guid DockerSkill = new("50000000-0000-4000-8000-000000000005");
        public static readonly Guid SqlSkill = new("50000000-0000-4000-8000-000000000006");
    }
}
