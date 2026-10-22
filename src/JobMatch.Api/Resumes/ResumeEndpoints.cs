using JobMatch.Domain;
using JobMatch.Infrastructure.Identity;
using JobMatch.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace JobMatch.Api.Resumes;

public static class ResumeEndpoints
{
    public static IEndpointRouteBuilder MapResumeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api")
            .WithTags("Resume")
            .RequireAuthorization("Candidate");

        group.MapGet("/resume/me", GetAsync)
            .WithName("getMyResume")
            .Produces<ResumeResponse>()
            .Produces(StatusCodes.Status404NotFound);

        group.MapPut("/resume/me", PutAsync)
            .WithName("putMyResume")
            .Produces<ResumeResponse>()
            .Produces<Api.Auth.ApiError>(StatusCodes.Status400BadRequest);

        return endpoints;
    }

    private static async Task<IResult> GetAsync(
        HttpContext context,
        UserManager<ApplicationUser> userManager,
        JobMatchDbContext dbContext)
    {
        var profile = await FindProfileAsync(context, userManager, dbContext);
        return profile?.Resume is null
            ? Results.NotFound(new Api.Auth.ApiError("resume_not_found", "Резюме ещё не заполнено."))
            : Results.Ok(ToResponse(profile));
    }

    private static async Task<IResult> PutAsync(
        ResumeInput input,
        HttpContext context,
        UserManager<ApplicationUser> userManager,
        JobMatchDbContext dbContext)
    {
        var error = Validate(input);
        if (error is not null)
        {
            return Results.BadRequest(new Api.Auth.ApiError("validation_error", error));
        }

        var profile = await FindProfileAsync(context, userManager, dbContext);
        if (profile is null)
        {
            return Results.NotFound(new Api.Auth.ApiError("profile_not_found", "Профиль соискателя не найден."));
        }

        profile.FullName = input.FullName.Trim();
        profile.Phone = input.ContactPhone.Trim();
        profile.UpdatedAt = DateTimeOffset.UtcNow;

        var resume = profile.Resume;
        var isNewResume = resume is null;
        if (isNewResume)
        {
            resume = new Resume { CandidateProfile = profile, CandidateProfileId = profile.Id };
            profile.Resume = resume;
        }
        else
        {
            dbContext.WorkExperiences.RemoveRange(resume!.WorkExperiences);
            dbContext.Educations.RemoveRange(resume.Educations);
            dbContext.ResumeSkills.RemoveRange(resume.ResumeSkills);
        }

        resume.ContactEmail = input.ContactEmail.Trim();
        resume.ContactPhone = input.ContactPhone.Trim();
        resume.DesiredPosition = input.DesiredPosition.Trim();
        resume.Summary = input.Summary.Trim();
        resume.UpdatedAt = DateTimeOffset.UtcNow;
        resume.WorkExperiences = input.WorkExperience.Select(item => new WorkExperience
        {
            CompanyName = item.CompanyName.Trim(),
            Position = item.Position.Trim(),
            StartedOn = item.StartedOn,
            EndedOn = item.EndedOn,
            Description = string.IsNullOrWhiteSpace(item.Description) ? null : item.Description.Trim(),
        }).ToList();
        resume.Educations = input.Education.Select(item => new Education
        {
            Institution = item.Institution.Trim(),
            Specialty = item.Specialty.Trim(),
            Degree = string.IsNullOrWhiteSpace(item.Degree) ? null : item.Degree.Trim(),
            GraduationYear = item.GraduationYear,
        }).ToList();

        var skillNames = input.Skills
            .Select(name => name.Trim())
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var normalizedNames = skillNames.Select(name => name.ToLower()).ToArray();
        var existingSkills = await dbContext.Skills
            .Where(skill => normalizedNames.Contains(skill.Name.ToLower()))
            .ToListAsync();
        resume.ResumeSkills = skillNames.Select(name =>
        {
            var skill = existingSkills.FirstOrDefault(item =>
                string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase)) ?? new Skill { Name = name };
            return new ResumeSkill { Resume = resume, Skill = skill };
        }).ToList();

        if (isNewResume) dbContext.Resumes.Add(resume);
        dbContext.WorkExperiences.AddRange(resume.WorkExperiences);
        dbContext.Educations.AddRange(resume.Educations);
        foreach (var link in resume.ResumeSkills)
        {
            if (dbContext.Entry(link.Skill).State == EntityState.Detached)
                dbContext.Skills.Add(link.Skill);
        }
        dbContext.ResumeSkills.AddRange(resume.ResumeSkills);

        await dbContext.SaveChangesAsync();
        return Results.Ok(ToResponse(profile));
    }

    private static async Task<CandidateProfile?> FindProfileAsync(
        HttpContext context,
        UserManager<ApplicationUser> userManager,
        JobMatchDbContext dbContext)
    {
        var userIdText = userManager.GetUserId(context.User);
        if (!Guid.TryParse(userIdText, out var userId)) return null;

        return await dbContext.CandidateProfiles
            .Include(profile => profile.Resume).ThenInclude(resume => resume!.WorkExperiences)
            .Include(profile => profile.Resume).ThenInclude(resume => resume!.Educations)
            .Include(profile => profile.Resume).ThenInclude(resume => resume!.ResumeSkills).ThenInclude(link => link.Skill)
            .SingleOrDefaultAsync(profile => profile.UserId == userId);
    }

    private static string? Validate(ResumeInput input)
    {
        if (string.IsNullOrWhiteSpace(input.FullName) || input.FullName.Trim().Length > 200)
            return "Укажите имя длиной до 200 символов.";
        if (string.IsNullOrWhiteSpace(input.ContactEmail) || !input.ContactEmail.Contains('@') || input.ContactEmail.Trim().Length > 320)
            return "Укажите корректный контактный email.";
        if (string.IsNullOrWhiteSpace(input.ContactPhone) || input.ContactPhone.Trim().Length > 32)
            return "Укажите контактный телефон длиной до 32 символов.";
        if (string.IsNullOrWhiteSpace(input.DesiredPosition) || input.DesiredPosition.Trim().Length > 200)
            return "Укажите желаемую должность длиной до 200 символов.";
        if (string.IsNullOrWhiteSpace(input.Summary)) return "Добавьте описание опыта.";
        if (input.Skills.Any(skill => skill.Trim().Length > 100)) return "Название навыка не должно превышать 100 символов.";
        if (input.WorkExperience.Any(item => string.IsNullOrWhiteSpace(item.CompanyName) || string.IsNullOrWhiteSpace(item.Position)))
            return "Укажите компанию и должность для каждого места работы.";
        if (input.WorkExperience.Any(item => item.EndedOn is not null && item.EndedOn < item.StartedOn))
            return "Дата окончания работы не может быть раньше даты начала.";
        if (input.Education.Any(item => string.IsNullOrWhiteSpace(item.Institution) || string.IsNullOrWhiteSpace(item.Specialty)))
            return "Укажите учебное заведение и специальность.";
        return null;
    }

    private static ResumeResponse ToResponse(CandidateProfile profile)
    {
        var resume = profile.Resume!;
        return new ResumeResponse(
            profile.FullName,
            resume.ContactEmail,
            resume.ContactPhone,
            resume.DesiredPosition,
            resume.Summary,
            resume.ResumeSkills.Select(link => link.Skill.Name).Order().ToArray(),
            resume.WorkExperiences.Select(item => new WorkExperienceInput(item.CompanyName, item.Position, item.StartedOn, item.EndedOn, item.Description)).ToArray(),
            resume.Educations.Select(item => new EducationInput(item.Institution, item.Specialty, item.Degree, item.GraduationYear)).ToArray());
    }
}

public sealed record ResumeInput(
    string FullName,
    string ContactEmail,
    string ContactPhone,
    string DesiredPosition,
    string Summary,
    string[] Skills,
    WorkExperienceInput[] WorkExperience,
    EducationInput[] Education);

public sealed record WorkExperienceInput(
    string CompanyName,
    string Position,
    DateOnly StartedOn,
    DateOnly? EndedOn,
    string? Description);

public sealed record EducationInput(
    string Institution,
    string Specialty,
    string? Degree,
    short? GraduationYear);

public sealed record ResumeResponse(
    string FullName,
    string ContactEmail,
    string ContactPhone,
    string DesiredPosition,
    string Summary,
    string[] Skills,
    WorkExperienceInput[] WorkExperience,
    EducationInput[] Education);
