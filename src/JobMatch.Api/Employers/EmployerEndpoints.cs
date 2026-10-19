using JobMatch.Api.Auth;
using JobMatch.Domain;
using JobMatch.Infrastructure.Identity;
using JobMatch.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace JobMatch.Api.Employers;

public static class EmployerEndpoints
{
    public static IEndpointRouteBuilder MapEmployerEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/vacancies/{vacancyId:guid}", GetPublishedVacancyAsync)
            .WithTags("Vacancies")
            .WithName("getVacancy")
            .Produces<VacancyResponse>();

        var employer = endpoints.MapGroup("/api").RequireAuthorization("Employer");
        employer.MapGet("/company/me", GetCompanyAsync).WithTags("Company").WithName("getMyCompany");
        employer.MapPut("/company/me", PutCompanyAsync).WithTags("Company").WithName("putMyCompany");
        employer.MapGet("/employer/vacancies", ListVacanciesAsync).WithTags("Vacancies").WithName("listEmployerVacancies");
        employer.MapPost("/employer/vacancies", CreateVacancyAsync).WithTags("Vacancies").WithName("createVacancy");
        employer.MapPut("/vacancies/{vacancyId:guid}", UpdateVacancyAsync).WithTags("Vacancies").WithName("updateVacancy");
        employer.MapPost("/employer/vacancies/{vacancyId:guid}/publish", PublishVacancyAsync).WithTags("Vacancies").WithName("publishVacancy");
        employer.MapPost("/employer/vacancies/{vacancyId:guid}/close", CloseVacancyAsync).WithTags("Vacancies").WithName("closeVacancy");
        return endpoints;
    }

    private static async Task<IResult> GetPublishedVacancyAsync(Guid vacancyId, JobMatchDbContext dbContext)
    {
        var vacancy = await dbContext.Vacancies.Include(item => item.Company)
            .SingleOrDefaultAsync(item => item.Id == vacancyId && item.Status == VacancyStatus.Published);
        return vacancy is null
            ? Results.NotFound(new ApiError("vacancy_not_found", "Опубликованная вакансия не найдена."))
            : Results.Ok(ToResponse(vacancy));
    }

    private static async Task<IResult> GetCompanyAsync(HttpContext context, UserManager<ApplicationUser> users, JobMatchDbContext dbContext)
    {
        var company = await FindCompanyAsync(context, users, dbContext);
        return company is null
            ? Results.NotFound(new ApiError("company_not_found", "Компания ещё не заполнена."))
            : Results.Ok(ToResponse(company));
    }

    private static async Task<IResult> PutCompanyAsync(CompanyInput input, HttpContext context, UserManager<ApplicationUser> users, JobMatchDbContext dbContext)
    {
        var validation = ValidateCompany(input);
        if (validation is not null) return Results.BadRequest(new ApiError("validation_error", validation));
        if (!Guid.TryParse(users.GetUserId(context.User), out var userId)) return Results.Unauthorized();

        var company = await FindCompanyAsync(context, users, dbContext);
        if (company is null)
        {
            company = new Company();
            company.Member = new CompanyMember { Company = company, UserId = userId };
            dbContext.Companies.Add(company);
        }
        company.Name = input.Name.Trim();
        company.Description = input.Description.Trim();
        company.City = input.City.Trim();
        company.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync();
        return Results.Ok(ToResponse(company));
    }

    private static async Task<IResult> ListVacanciesAsync(HttpContext context, UserManager<ApplicationUser> users, JobMatchDbContext dbContext)
    {
        var company = await FindCompanyAsync(context, users, dbContext);
        if (company is null) return Results.Ok(Array.Empty<VacancyResponse>());
        var vacancies = await dbContext.Vacancies.Include(item => item.Company)
            .Where(item => item.CompanyId == company.Id)
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync();
        return Results.Ok(vacancies.Select(ToResponse));
    }

    private static async Task<IResult> CreateVacancyAsync(VacancyInput input, HttpContext context, UserManager<ApplicationUser> users, JobMatchDbContext dbContext)
    {
        var validation = ValidateVacancy(input);
        if (validation is not null) return Results.BadRequest(new ApiError("validation_error", validation));
        var company = await FindCompanyAsync(context, users, dbContext);
        if (company is null) return Results.Conflict(new ApiError("company_required", "Сначала заполните компанию."));
        var vacancy = new Vacancy { Company = company, CompanyId = company.Id, Status = VacancyStatus.Draft };
        Apply(input, vacancy);
        dbContext.Vacancies.Add(vacancy);
        await dbContext.SaveChangesAsync();
        return Results.Created($"/api/vacancies/{vacancy.Id}", ToResponse(vacancy));
    }

    private static async Task<IResult> UpdateVacancyAsync(Guid vacancyId, VacancyInput input, HttpContext context, UserManager<ApplicationUser> users, JobMatchDbContext dbContext)
    {
        var validation = ValidateVacancy(input);
        if (validation is not null) return Results.BadRequest(new ApiError("validation_error", validation));
        var vacancy = await FindOwnedVacancyAsync(vacancyId, context, users, dbContext);
        if (vacancy is null) return Results.NotFound(new ApiError("vacancy_not_found", "Вакансия не найдена."));
        if (vacancy.Status != VacancyStatus.Draft) return Results.Conflict(new ApiError("vacancy_not_draft", "Редактировать можно только черновик."));
        Apply(input, vacancy);
        await dbContext.SaveChangesAsync();
        return Results.Ok(ToResponse(vacancy));
    }

    private static async Task<IResult> PublishVacancyAsync(Guid vacancyId, HttpContext context, UserManager<ApplicationUser> users, JobMatchDbContext dbContext)
    {
        var vacancy = await FindOwnedVacancyAsync(vacancyId, context, users, dbContext);
        if (vacancy is null) return Results.NotFound(new ApiError("vacancy_not_found", "Вакансия не найдена."));
        if (vacancy.Status != VacancyStatus.Draft) return Results.Conflict(new ApiError("invalid_vacancy_status", "Опубликовать можно только черновик."));
        vacancy.Status = VacancyStatus.Published;
        vacancy.PublishedAt = DateTimeOffset.UtcNow;
        vacancy.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync();
        return Results.Ok(ToResponse(vacancy));
    }

    private static async Task<IResult> CloseVacancyAsync(Guid vacancyId, HttpContext context, UserManager<ApplicationUser> users, JobMatchDbContext dbContext)
    {
        var vacancy = await FindOwnedVacancyAsync(vacancyId, context, users, dbContext);
        if (vacancy is null) return Results.NotFound(new ApiError("vacancy_not_found", "Вакансия не найдена."));
        if (vacancy.Status != VacancyStatus.Published) return Results.Conflict(new ApiError("invalid_vacancy_status", "Закрыть можно только опубликованную вакансию."));
        vacancy.Status = VacancyStatus.Closed;
        vacancy.ClosedAt = DateTimeOffset.UtcNow;
        vacancy.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync();
        return Results.Ok(ToResponse(vacancy));
    }

    private static async Task<Company?> FindCompanyAsync(HttpContext context, UserManager<ApplicationUser> users, JobMatchDbContext dbContext)
    {
        if (!Guid.TryParse(users.GetUserId(context.User), out var userId)) return null;
        return await dbContext.Companies.Include(item => item.Member)
            .SingleOrDefaultAsync(item => item.Member != null && item.Member.UserId == userId);
    }

    private static async Task<Vacancy?> FindOwnedVacancyAsync(Guid id, HttpContext context, UserManager<ApplicationUser> users, JobMatchDbContext dbContext)
    {
        var company = await FindCompanyAsync(context, users, dbContext);
        if (company is null) return null;
        return await dbContext.Vacancies.Include(item => item.Company)
            .SingleOrDefaultAsync(item => item.Id == id && item.CompanyId == company.Id);
    }

    private static void Apply(VacancyInput input, Vacancy vacancy)
    {
        vacancy.Title = input.Title.Trim();
        vacancy.Description = input.Description.Trim();
        vacancy.Requirements = input.Requirements.Trim();
        vacancy.City = input.City.Trim();
        vacancy.WorkFormat = input.WorkFormat;
        vacancy.SalaryFrom = input.SalaryFrom;
        vacancy.SalaryTo = input.SalaryTo;
        vacancy.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static string? ValidateCompany(CompanyInput input) =>
        string.IsNullOrWhiteSpace(input.Name) || input.Name.Trim().Length > 200 ? "Укажите название компании длиной до 200 символов." :
        string.IsNullOrWhiteSpace(input.Description) ? "Добавьте описание компании." :
        string.IsNullOrWhiteSpace(input.City) || input.City.Trim().Length > 120 ? "Укажите город длиной до 120 символов." : null;

    private static string? ValidateVacancy(VacancyInput input) =>
        string.IsNullOrWhiteSpace(input.Title) || input.Title.Trim().Length > 200 ? "Укажите название вакансии длиной до 200 символов." :
        string.IsNullOrWhiteSpace(input.Description) ? "Добавьте описание вакансии." :
        string.IsNullOrWhiteSpace(input.Requirements) ? "Добавьте требования." :
        string.IsNullOrWhiteSpace(input.City) || input.City.Trim().Length > 120 ? "Укажите город длиной до 120 символов." :
        input.SalaryFrom < 0 || input.SalaryTo < 0 || (input.SalaryFrom is not null && input.SalaryTo is not null && input.SalaryFrom > input.SalaryTo) ? "Проверьте диапазон зарплаты." : null;

    private static CompanyResponse ToResponse(Company company) => new(company.Id, company.Name, company.Description, company.City);
    private static VacancyResponse ToResponse(Vacancy vacancy) => new(vacancy.Id, vacancy.Title, vacancy.Description, vacancy.Requirements, vacancy.City, vacancy.WorkFormat, vacancy.SalaryFrom, vacancy.SalaryTo, vacancy.Status.ToString(), new CompanySummary(vacancy.CompanyId, vacancy.Company.Name), vacancy.PublishedAt);
}

public sealed record CompanyInput(string Name, string Description, string City);
public sealed record CompanyResponse(Guid Id, string Name, string Description, string City);
public sealed record VacancyInput(string Title, string Description, string Requirements, string City, WorkFormat WorkFormat, int? SalaryFrom, int? SalaryTo);
public sealed record VacancyResponse(Guid Id, string Title, string Description, string Requirements, string City, WorkFormat WorkFormat, int? SalaryFrom, int? SalaryTo, string Status, CompanySummary Company, DateTimeOffset? PublishedAt);
public sealed record CompanySummary(Guid Id, string Name);
