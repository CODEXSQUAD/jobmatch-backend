using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JobMatch.Infrastructure.Persistence;
using JobMatch.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace JobMatch.Infrastructure.Tests;

public sealed class AuthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    public AuthEndpointTests(WebApplicationFactory<Program> factory)
    {
        var databaseName = $"auth-{Guid.NewGuid()}";
        this.factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DbContextOptions<JobMatchDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<JobMatchDbContext>>();
                services.RemoveAll<JobMatchDbContext>();
                var options = new DbContextOptionsBuilder<JobMatchDbContext>()
                    .UseInMemoryDatabase(databaseName)
                    .Options;
                services.AddSingleton(options);
                services.AddScoped<JobMatchDbContext>(_ => new TestJobMatchDbContext(options));
            });
        });
    }

    [Fact]
    public async Task Candidate_CanRegisterReadSessionAndLogout()
    {
        using var client = factory.CreateClient();

        var registration = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = "candidate@example.test",
            password = "ExamplePassword123!",
            role = "Candidate",
        });
        var registeredUser = await registration.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        Assert.Equal("candidate@example.test", registeredUser.GetProperty("email").GetString());
        Assert.Equal("Candidate", registeredUser.GetProperty("role").GetString());

        var currentUser = await client.GetAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.OK, currentUser.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<JobMatchDbContext>();
            var profile = await dbContext.CandidateProfiles
                .Include(item => item.CreditAccount)
                .SingleAsync();
            Assert.Equal(5, profile.CreditAccount?.Balance);
            Assert.Equal(5, profile.CreditAccount?.DailyLimit);
        }

        var logout = await client.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/users/me")).StatusCode);
    }

    [Fact]
    public async Task ExistingUser_CanLoginAndDuplicateRegistrationIsRejected()
    {
        using var registrationClient = factory.CreateClient();
        var request = new
        {
            email = "employer@example.test",
            password = "ExamplePassword123!",
            role = "Employer",
        };

        Assert.Equal(
            HttpStatusCode.Created,
            (await registrationClient.PostAsJsonAsync("/api/auth/register", request)).StatusCode);

        using var loginClient = factory.CreateClient();
        var login = await loginClient.PostAsJsonAsync("/api/auth/login", new
        {
            request.email,
            request.password,
        });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await loginClient.GetAsync("/api/users/me")).StatusCode);

        var duplicate = await loginClient.PostAsJsonAsync("/api/auth/register", request);
        var error = await duplicate.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("email_already_registered", error.GetProperty("code").GetString());
    }

    [Fact]
    public async Task InvalidCredentialsAndAnonymousSession_ReturnUnauthorized()
    {
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/users/me")).StatusCode);

        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "missing@example.test",
            password = "WrongPassword123!",
        });
        var error = await login.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        Assert.Equal("invalid_credentials", error.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Candidate_CanCreateAndReadResume()
    {
        using var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = "resume@example.test",
            password = "ExamplePassword123!",
            role = "Candidate",
        });

        var payload = new
        {
            fullName = "Иван Петров",
            contactEmail = "resume@example.test",
            contactPhone = "+70000000000",
            desiredPosition = "Backend-разработчик",
            summary = "Разрабатываю учебные приложения.",
            skills = new[] { "C#", "PostgreSQL" },
            workExperience = new[]
            {
                new { companyName = "Учебная компания", position = "Стажёр", startedOn = "2025-01-01", endedOn = (string?)null, description = "Backend" },
            },
            education = new[]
            {
                new { institution = "Учебный университет", specialty = "Разработка ПО", degree = "Бакалавр", graduationYear = 2027 },
            },
        };

        var saved = await client.PutAsJsonAsync("/api/resume/me", payload);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);

        var response = await client.GetAsync("/api/resume/me");
        var resume = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Иван Петров", resume.GetProperty("fullName").GetString());
        Assert.Equal(2, resume.GetProperty("skills").GetArrayLength());
        Assert.Single(resume.GetProperty("workExperience").EnumerateArray());
    }

    [Fact]
    public async Task Employer_CannotAccessCandidateResume()
    {
        using var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = "resume-employer@example.test",
            password = "ExamplePassword123!",
            role = "Employer",
        });

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/resume/me")).StatusCode);
    }

    [Fact]
    public async Task CandidateApplication_DebitsOnceAndSafeRetryReturnsExistingResult()
    {
        using var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = "application@example.test",
            password = "ExamplePassword123!",
            role = "Candidate",
        });
        await client.PutAsJsonAsync("/api/resume/me", new
        {
            fullName = "Анна Смирнова",
            contactEmail = "application@example.test",
            contactPhone = "+70000000001",
            desiredPosition = "Разработчик",
            summary = "Учебное резюме",
            skills = new[] { "C#" },
            workExperience = Array.Empty<object>(),
            education = Array.Empty<object>(),
        });

        var vacancyId = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<JobMatchDbContext>();
            var company = new Company { Name = "Компания", Description = "Описание", City = "Москва" };
            dbContext.Add(company);
            dbContext.Add(new Vacancy
            {
                Id = vacancyId,
                Company = company,
                Title = "Разработчик",
                Description = "Описание",
                Requirements = "Требования",
                City = "Москва",
                WorkFormat = WorkFormat.Hybrid,
                Status = VacancyStatus.Published,
                PublishedAt = DateTimeOffset.UtcNow,
            });
            await dbContext.SaveChangesAsync();
        }

        using var firstRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/vacancies/{vacancyId}/applications")
        {
            Content = JsonContent.Create(new { coverLetter = "Хочу в команду" }),
        };
        firstRequest.Headers.Add("Idempotency-Key", "application-test-key");
        var first = await client.SendAsync(firstRequest);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        using var retryRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/vacancies/{vacancyId}/applications")
        {
            Content = JsonContent.Create(new { coverLetter = "Хочу в команду" }),
        };
        retryRequest.Headers.Add("Idempotency-Key", "application-test-key");
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(retryRequest)).StatusCode);

        var credits = await client.GetFromJsonAsync<JsonElement>("/api/credits/me");
        Assert.Equal(4, credits.GetProperty("balance").GetInt32());
    }

    [Fact]
    public async Task Employer_CanCreateCompanyAndRunVacancyLifecycle()
    {
        using var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = "vacancy-employer@example.test",
            password = "ExamplePassword123!",
            role = "Employer",
        });

        var company = await client.PutAsJsonAsync("/api/company/me", new
        {
            name = "Новая компания",
            description = "Учебная компания",
            city = "Москва",
        });
        Assert.Equal(HttpStatusCode.OK, company.StatusCode);

        var draftResponse = await client.PostAsJsonAsync("/api/employer/vacancies", new
        {
            title = "Backend-разработчик",
            description = "Разработка JobMatch",
            requirements = "C# и PostgreSQL",
            city = "Москва",
            workFormat = "Hybrid",
            salaryFrom = 120000,
            salaryTo = 180000,
        });
        var draft = await draftResponse.Content.ReadFromJsonAsync<JsonElement>();
        var vacancyId = draft.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.Created, draftResponse.StatusCode);
        Assert.Equal("Draft", draft.GetProperty("status").GetString());

        var publish = await client.PostAsync($"/api/employer/vacancies/{vacancyId}/publish", null);
        Assert.Equal(HttpStatusCode.OK, publish.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/vacancies/{vacancyId}")).StatusCode);

        var close = await client.PostAsync($"/api/employer/vacancies/{vacancyId}/close", null);
        Assert.Equal(HttpStatusCode.OK, close.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/vacancies/{vacancyId}")).StatusCode);
    }
}
