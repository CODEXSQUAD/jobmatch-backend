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
    public async Task ApplicationResumeSnapshot_UsesCamelCaseReadsLegacyPascalCaseAndStaysImmutable()
    {
        using var employerClient = factory.CreateClient();
        await employerClient.PostAsJsonAsync("/api/auth/register", new
        {
            email = "snapshot-employer@example.test",
            password = "ExamplePassword123!",
            role = "Employer",
        });
        await employerClient.PutAsJsonAsync("/api/company/me", new
        {
            name = "Компания снимков",
            description = "Проверка формата снимков резюме",
            city = "Москва",
        });

        async Task<Guid> CreatePublishedVacancyAsync(string title)
        {
            var draftResponse = await employerClient.PostAsJsonAsync("/api/employer/vacancies", new
            {
                title,
                description = "Описание вакансии",
                requirements = "Требования",
                city = "Москва",
                workFormat = "Remote",
                salaryFrom = 100_000,
                salaryTo = 150_000,
            });
            Assert.Equal(HttpStatusCode.Created, draftResponse.StatusCode);
            var draft = await draftResponse.Content.ReadFromJsonAsync<JsonElement>();
            var vacancyId = draft.GetProperty("id").GetGuid();
            Assert.Equal(HttpStatusCode.OK, (await employerClient.PostAsync($"/api/employer/vacancies/{vacancyId}/publish", null)).StatusCode);
            return vacancyId;
        }

        var newFormatVacancyId = await CreatePublishedVacancyAsync("Новый формат");
        var legacyFormatVacancyId = await CreatePublishedVacancyAsync("Старый формат");

        using var candidateClient = factory.CreateClient();
        await candidateClient.PostAsJsonAsync("/api/auth/register", new
        {
            email = "snapshot-candidate@example.test",
            password = "ExamplePassword123!",
            role = "Candidate",
        });
        var originalResume = new
        {
            fullName = "Анна Снимкова",
            contactEmail = "snapshot-candidate@example.test",
            contactPhone = "+70000000002",
            desiredPosition = "Backend-разработчик",
            summary = "Исходное описание",
            skills = new[] { "C#", "PostgreSQL" },
            workExperience = new[]
            {
                new { companyName = "Первая компания", position = "Разработчик", startedOn = "2024-01-01", endedOn = (string?)null, description = "API" },
            },
            education = new[]
            {
                new { institution = "Университет", specialty = "Разработка ПО", degree = "Бакалавр", graduationYear = 2025 },
            },
        };
        Assert.Equal(HttpStatusCode.OK, (await candidateClient.PutAsJsonAsync("/api/resume/me", originalResume)).StatusCode);

        using (var request = new HttpRequestMessage(HttpMethod.Post, $"/api/vacancies/{newFormatVacancyId}/applications")
        {
            Content = JsonContent.Create(new { coverLetter = "Новый снимок" }),
        })
        {
            request.Headers.Add("Idempotency-Key", "snapshot-new-format");
            Assert.Equal(HttpStatusCode.Created, (await candidateClient.SendAsync(request)).StatusCode);
        }

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<JobMatchDbContext>();
            var application = await dbContext.Applications.SingleAsync(item => item.VacancyId == newFormatVacancyId);
            var storedSnapshot = application.ResumeSnapshot.RootElement;
            Assert.True(storedSnapshot.TryGetProperty("fullName", out _));
            Assert.False(storedSnapshot.TryGetProperty("FullName", out _));
            Assert.True(storedSnapshot.GetProperty("workExperience")[0].TryGetProperty("companyName", out _));
            Assert.True(storedSnapshot.GetProperty("education")[0].TryGetProperty("institution", out _));
            Assert.Equal(2, application.SnapshotVersion);
        }

        Assert.Equal(HttpStatusCode.OK, (await candidateClient.PutAsJsonAsync("/api/resume/me", new
        {
            fullName = "Анна После Редактирования",
            contactEmail = "changed@example.test",
            contactPhone = "+70000000003",
            desiredPosition = "Изменённая должность",
            summary = "Изменённое описание",
            skills = new[] { "Go" },
            workExperience = Array.Empty<object>(),
            education = Array.Empty<object>(),
        })).StatusCode);

        var newFormatApplications = await employerClient.GetFromJsonAsync<JsonElement>($"/api/vacancies/{newFormatVacancyId}/applications");
        var immutableSnapshot = newFormatApplications[0].GetProperty("resumeSnapshot");
        Assert.Equal("Анна Снимкова", immutableSnapshot.GetProperty("fullName").GetString());
        Assert.Equal("Backend-разработчик", immutableSnapshot.GetProperty("desiredPosition").GetString());
        Assert.Equal("C#", immutableSnapshot.GetProperty("skills")[0].GetString());
        Assert.Equal("Первая компания", immutableSnapshot.GetProperty("workExperience")[0].GetProperty("companyName").GetString());
        Assert.Equal("Университет", immutableSnapshot.GetProperty("education")[0].GetProperty("institution").GetString());

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<JobMatchDbContext>();
            var profile = await dbContext.CandidateProfiles.Include(item => item.Resume).SingleAsync();
            dbContext.Applications.Add(new JobApplication
            {
                CandidateProfileId = profile.Id,
                ResumeId = profile.Resume!.Id,
                VacancyId = legacyFormatVacancyId,
                IdempotencyKey = "snapshot-legacy-format",
                ResumeSnapshot = JsonDocument.Parse("""
                    {
                      "FullName": "Легаси Кандидат",
                      "ContactEmail": "legacy@example.test",
                      "ContactPhone": "+70000000004",
                      "DesiredPosition": "Legacy-разработчик",
                      "Summary": "Старый снимок",
                      "Skills": ["Legacy skill"],
                      "WorkExperience": [{
                        "CompanyName": "Старая компания",
                        "Position": "Разработчик",
                        "StartedOn": "2020-01-01",
                        "EndedOn": null,
                        "Description": "Старый опыт"
                      }],
                      "Education": [{
                        "Institution": "Старый университет",
                        "Specialty": "Информатика",
                        "Degree": "Специалист",
                        "GraduationYear": 2020
                      }]
                    }
                    """),
                SnapshotVersion = 1,
            });
            await dbContext.SaveChangesAsync();
        }

        var legacyApplications = await employerClient.GetFromJsonAsync<JsonElement>($"/api/vacancies/{legacyFormatVacancyId}/applications");
        var normalizedLegacySnapshot = legacyApplications[0].GetProperty("resumeSnapshot");
        Assert.Equal("Легаси Кандидат", normalizedLegacySnapshot.GetProperty("fullName").GetString());
        Assert.Equal("Legacy skill", normalizedLegacySnapshot.GetProperty("skills")[0].GetString());
        Assert.Equal("Старая компания", normalizedLegacySnapshot.GetProperty("workExperience")[0].GetProperty("companyName").GetString());
        Assert.Equal("Старый университет", normalizedLegacySnapshot.GetProperty("education")[0].GetProperty("institution").GetString());
        Assert.False(normalizedLegacySnapshot.TryGetProperty("FullName", out _));
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
