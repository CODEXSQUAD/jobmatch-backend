using System.Text.Json;
using JobMatch.Api.Auth;
using JobMatch.Api.Resumes;
using JobMatch.Domain;
using JobMatch.Infrastructure.Identity;
using JobMatch.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace JobMatch.Api.Applications;

public static class ApplicationEndpoints
{
    public static IEndpointRouteBuilder MapApplicationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var candidate = endpoints.MapGroup("/api").WithTags("Applications").RequireAuthorization("Candidate");

        candidate.MapPost("/vacancies/{vacancyId:guid}/applications", CreateAsync)
            .WithName("createApplication")
            .Produces<ApplicationResponse>(StatusCodes.Status201Created)
            .Produces<ApplicationResponse>()
            .Produces<ApiError>(StatusCodes.Status409Conflict);
        candidate.MapGet("/applications/me", ListMineAsync)
            .WithName("listMyApplications")
            .Produces<ApplicationResponse[]>();
        candidate.MapPost("/applications/{applicationId:guid}/withdraw", WithdrawAsync)
            .WithName("withdrawApplication")
            .Produces<ApplicationResponse>()
            .Produces<ApiError>(StatusCodes.Status409Conflict);
        candidate.MapGet("/credits/me", GetCreditsAsync)
            .WithName("getMyCredits")
            .Produces<CreditBalanceResponse>();

        var employer = endpoints.MapGroup("/api").WithTags("Applications").RequireAuthorization("Employer");
        employer.MapGet("/vacancies/{vacancyId:guid}/applications", ListForVacancyAsync)
            .WithName("listVacancyApplications")
            .Produces<ApplicationResponse[]>();
        employer.MapPatch("/employer/applications/{applicationId:guid}/status", UpdateStatusAsync)
            .WithName("updateApplicationStatus")
            .Produces<ApplicationResponse>()
            .Produces<ApiError>(StatusCodes.Status409Conflict);

        return endpoints;
    }

    private static async Task<IResult> CreateAsync(
        Guid vacancyId,
        ApplicationInput input,
        HttpContext context,
        UserManager<ApplicationUser> userManager,
        JobMatchDbContext dbContext)
    {
        var key = context.Request.Headers["Idempotency-Key"].ToString().Trim();
        if (key.Length is 0 or > 100)
            return Results.BadRequest(new ApiError("validation_error", "Передайте Idempotency-Key длиной до 100 символов."));

        var profile = await GetCandidateAsync(context, userManager, dbContext, includeResume: true);
        if (profile?.Resume is null)
            return Results.Conflict(new ApiError("resume_required", "Сначала заполните резюме."));

        var repeated = await dbContext.Applications
            .SingleOrDefaultAsync(item => item.CandidateProfileId == profile.Id && item.IdempotencyKey == key);
        if (repeated is not null)
        {
            return repeated.VacancyId == vacancyId
                ? Results.Ok(ToResponse(repeated))
                : Results.Conflict(new ApiError("idempotency_conflict", "Этот ключ уже использован для другого отклика."));
        }

        if (await dbContext.Applications.AnyAsync(item => item.CandidateProfileId == profile.Id && item.VacancyId == vacancyId))
            return Results.Conflict(new ApiError("application_already_exists", "Вы уже откликались на эту вакансию."));

        var vacancy = await dbContext.Vacancies.SingleOrDefaultAsync(item => item.Id == vacancyId);
        if (vacancy is null || vacancy.Status != VacancyStatus.Published)
            return Results.NotFound(new ApiError("vacancy_not_found", "Опубликованная вакансия не найдена."));

        var account = profile.CreditAccount;
        if (account is null)
            return Results.Conflict(new ApiError("credit_account_not_found", "Счёт откликов не найден."));

        var today = MoscowToday();
        if (account.PeriodDate != today)
        {
            account.PeriodDate = today;
            account.Balance = account.DailyLimit;
            account.Version++;
            account.Transactions.Add(new CreditTransaction
            {
                Type = CreditTransactionType.DailyReset,
                Amount = account.DailyLimit,
                Reason = "daily_reset",
            });
        }
        if (account.Balance <= 0)
            return Results.Conflict(new ApiError("credit_limit_exhausted", "Лимит откликов на сегодня исчерпан."));

        var snapshot = Snapshot(profile);
        var application = new JobApplication
        {
            CandidateProfile = profile,
            CandidateProfileId = profile.Id,
            Resume = profile.Resume,
            ResumeId = profile.Resume.Id,
            Vacancy = vacancy,
            VacancyId = vacancy.Id,
            IdempotencyKey = key,
            CoverLetter = string.IsNullOrWhiteSpace(input.CoverLetter) ? null : input.CoverLetter.Trim(),
            ResumeSnapshot = JsonSerializer.SerializeToDocument(snapshot),
        };
        account.Balance--;
        account.Version++;
        account.UpdatedAt = DateTimeOffset.UtcNow;
        var debit = new CreditTransaction
        {
            CreditAccount = account,
            Application = application,
            Type = CreditTransactionType.Debit,
            Amount = 1,
            Reason = "application",
        };

        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync()
            : null;
        dbContext.Applications.Add(application);
        dbContext.CreditTransactions.Add(debit);
        try
        {
            await dbContext.SaveChangesAsync();
            if (transaction is not null) await transaction.CommitAsync();
        }
        catch (DbUpdateException)
        {
            if (transaction is not null) await transaction.RollbackAsync();
            return Results.Conflict(new ApiError("application_conflict", "Отклик уже создан или лимит изменился. Обновите страницу."));
        }

        return Results.Created($"/api/applications/{application.Id}", ToResponse(application));
    }

    private static async Task<IResult> ListMineAsync(
        HttpContext context,
        UserManager<ApplicationUser> userManager,
        JobMatchDbContext dbContext)
    {
        var profile = await GetCandidateAsync(context, userManager, dbContext, includeResume: false);
        if (profile is null) return Results.Ok(Array.Empty<ApplicationResponse>());
        var applications = await dbContext.Applications
            .Where(item => item.CandidateProfileId == profile.Id)
            .OrderByDescending(item => item.SubmittedAt)
            .ToListAsync();
        return Results.Ok(applications.Select(ToResponse));
    }

    private static async Task<IResult> WithdrawAsync(
        Guid applicationId,
        HttpContext context,
        UserManager<ApplicationUser> userManager,
        JobMatchDbContext dbContext)
    {
        var profile = await GetCandidateAsync(context, userManager, dbContext, includeResume: false);
        var application = profile is null ? null : await dbContext.Applications
            .SingleOrDefaultAsync(item => item.Id == applicationId && item.CandidateProfileId == profile.Id);
        if (application is null)
            return Results.NotFound(new ApiError("application_not_found", "Отклик не найден."));
        if (application.Status is ApplicationStatus.Rejected or ApplicationStatus.Withdrawn)
            return Results.Conflict(new ApiError("invalid_application_status", "Этот отклик уже находится в конечном статусе."));
        application.Status = ApplicationStatus.Withdrawn;
        application.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync();
        return Results.Ok(ToResponse(application));
    }

    private static async Task<IResult> GetCreditsAsync(
        HttpContext context,
        UserManager<ApplicationUser> userManager,
        JobMatchDbContext dbContext)
    {
        var profile = await GetCandidateAsync(context, userManager, dbContext, includeResume: false);
        var account = profile?.CreditAccount;
        if (account is null) return Results.NotFound(new ApiError("credit_account_not_found", "Счёт откликов не найден."));
        var today = MoscowToday();
        var balance = account.PeriodDate == today ? account.Balance : account.DailyLimit;
        return Results.Ok(new CreditBalanceResponse(today, balance, account.DailyLimit));
    }

    private static async Task<IResult> ListForVacancyAsync(
        Guid vacancyId,
        HttpContext context,
        UserManager<ApplicationUser> userManager,
        JobMatchDbContext dbContext)
    {
        var companyId = await GetEmployerCompanyIdAsync(context, userManager, dbContext);
        if (companyId is null || !await dbContext.Vacancies.AnyAsync(item => item.Id == vacancyId && item.CompanyId == companyId))
            return Results.NotFound(new ApiError("vacancy_not_found", "Вакансия не найдена."));
        var applications = await dbContext.Applications
            .Where(item => item.VacancyId == vacancyId)
            .OrderByDescending(item => item.SubmittedAt)
            .ToListAsync();
        return Results.Ok(applications.Select(ToResponse));
    }

    private static async Task<IResult> UpdateStatusAsync(
        Guid applicationId,
        ApplicationStatusInput input,
        HttpContext context,
        UserManager<ApplicationUser> userManager,
        JobMatchDbContext dbContext)
    {
        if (!Enum.TryParse<ApplicationStatus>(input.Status, true, out var target) ||
            target is ApplicationStatus.Submitted or ApplicationStatus.Withdrawn)
            return Results.BadRequest(new ApiError("validation_error", "Статус должен быть Viewed, Invited или Rejected."));

        var companyId = await GetEmployerCompanyIdAsync(context, userManager, dbContext);
        var application = companyId is null ? null : await dbContext.Applications
            .Include(item => item.Vacancy)
            .SingleOrDefaultAsync(item => item.Id == applicationId && item.Vacancy.CompanyId == companyId);
        if (application is null)
            return Results.NotFound(new ApiError("application_not_found", "Отклик не найден."));

        var allowed = (application.Status, target) switch
        {
            (ApplicationStatus.Submitted, ApplicationStatus.Viewed) => true,
            (ApplicationStatus.Submitted or ApplicationStatus.Viewed, ApplicationStatus.Invited) => true,
            (ApplicationStatus.Submitted or ApplicationStatus.Viewed or ApplicationStatus.Invited, ApplicationStatus.Rejected) => true,
            _ => false,
        };
        if (!allowed)
            return Results.Conflict(new ApiError("invalid_application_status", "Недопустимый переход статуса отклика."));

        application.Status = target;
        application.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync();
        return Results.Ok(ToResponse(application));
    }

    private static async Task<Guid?> GetEmployerCompanyIdAsync(
        HttpContext context,
        UserManager<ApplicationUser> userManager,
        JobMatchDbContext dbContext)
    {
        if (!Guid.TryParse(userManager.GetUserId(context.User), out var userId)) return null;
        return await dbContext.CompanyMembers
            .Where(item => item.UserId == userId)
            .Select(item => (Guid?)item.CompanyId)
            .SingleOrDefaultAsync();
    }

    private static async Task<CandidateProfile?> GetCandidateAsync(
        HttpContext context,
        UserManager<ApplicationUser> userManager,
        JobMatchDbContext dbContext,
        bool includeResume)
    {
        if (!Guid.TryParse(userManager.GetUserId(context.User), out var userId)) return null;
        IQueryable<CandidateProfile> query = dbContext.CandidateProfiles.Include(item => item.CreditAccount);
        if (includeResume)
        {
            query = query
                .Include(item => item.Resume).ThenInclude(resume => resume!.WorkExperiences)
                .Include(item => item.Resume).ThenInclude(resume => resume!.Educations)
                .Include(item => item.Resume).ThenInclude(resume => resume!.ResumeSkills).ThenInclude(link => link.Skill);
        }
        return await query.SingleOrDefaultAsync(item => item.UserId == userId);
    }

    private static ResumeResponse Snapshot(CandidateProfile profile)
    {
        var resume = profile.Resume!;
        return new ResumeResponse(
            profile.FullName, resume.ContactEmail, resume.ContactPhone, resume.DesiredPosition, resume.Summary,
            resume.ResumeSkills.Select(link => link.Skill.Name).Order().ToArray(),
            resume.WorkExperiences.Select(item => new WorkExperienceInput(item.CompanyName, item.Position, item.StartedOn, item.EndedOn, item.Description)).ToArray(),
            resume.Educations.Select(item => new EducationInput(item.Institution, item.Specialty, item.Degree, item.GraduationYear)).ToArray());
    }

    private static ApplicationResponse ToResponse(JobApplication application) => new(
        application.Id,
        application.VacancyId,
        application.Status.ToString(),
        application.CoverLetter,
        application.ResumeSnapshot.RootElement.Clone(),
        application.SnapshotVersion,
        application.SubmittedAt);

    private static DateOnly MoscowToday()
    {
        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Moscow"); }
        catch (TimeZoneNotFoundException) { zone = TimeZoneInfo.FindSystemTimeZoneById("Russian Standard Time"); }
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone).DateTime);
    }
}

public sealed record ApplicationInput(string? CoverLetter);
public sealed record ApplicationStatusInput(string? Status);
public sealed record ApplicationResponse(Guid Id, Guid VacancyId, string Status, string? CoverLetter, JsonElement ResumeSnapshot, int SnapshotVersion, DateTimeOffset SubmittedAt);
public sealed record CreditBalanceResponse(DateOnly PeriodDate, int Balance, int DailyLimit);
