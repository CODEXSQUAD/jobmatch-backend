using JobMatch.Application.Vacancies;
using JobMatch.Domain;
using JobMatch.Infrastructure.Persistence;
using JobMatch.Infrastructure.Vacancies;
using Microsoft.EntityFrameworkCore;

namespace JobMatch.Infrastructure.Tests;

public sealed class VacancyCatalogTests
{
    [Fact]
    public async Task GetPageAsync_ReturnsOnlyPublishedVacanciesInStableOrder()
    {
        await using var dbContext = CreateDbContext();
        var company = CreateCompany();
        dbContext.Companies.Add(company);
        dbContext.Vacancies.AddRange(
            CreateVacancy(company, "Old", VacancyStatus.Published, 1),
            CreateVacancy(company, "Newest", VacancyStatus.Published, 3),
            CreateVacancy(company, "Draft", VacancyStatus.Draft, null),
            CreateVacancy(company, "Closed", VacancyStatus.Closed, 4));
        await dbContext.SaveChangesAsync();

        var result = await new VacancyCatalog(dbContext).GetPageAsync(
            new VacancyListQuery(null, null, null, 1, 20));

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(1, result.TotalPages);
        Assert.Equal(["Newest", "Old"], result.Items.Select(item => item.Title));
    }

    [Fact]
    public async Task GetPageAsync_AppliesFiltersWithoutCaseSensitivity()
    {
        await using var dbContext = CreateDbContext();
        var company = CreateCompany();
        dbContext.Companies.Add(company);
        dbContext.Vacancies.AddRange(
            CreateVacancy(company, "Backend-разработчик C#", VacancyStatus.Published, 3, "Москва", WorkFormat.Remote),
            CreateVacancy(company, "Frontend-разработчик", VacancyStatus.Published, 2, "Москва", WorkFormat.Remote),
            CreateVacancy(company, "Backend-разработчик", VacancyStatus.Published, 1, "Казань", WorkFormat.Office));
        await dbContext.SaveChangesAsync();

        var result = await new VacancyCatalog(dbContext).GetPageAsync(
            new VacancyListQuery("BACKEND", "москва", WorkFormat.Remote, 1, 20));

        var item = Assert.Single(result.Items);
        Assert.Equal("Backend-разработчик C#", item.Title);
        Assert.Equal(company.Id, item.Company.Id);
    }

    [Fact]
    public async Task GetPageAsync_PaginatesAndReturnsEmptyPagePastTheEnd()
    {
        await using var dbContext = CreateDbContext();
        var company = CreateCompany();
        dbContext.Companies.Add(company);
        dbContext.Vacancies.AddRange(
            CreateVacancy(company, "Third", VacancyStatus.Published, 3),
            CreateVacancy(company, "Second", VacancyStatus.Published, 2),
            CreateVacancy(company, "First", VacancyStatus.Published, 1));
        await dbContext.SaveChangesAsync();

        var catalog = new VacancyCatalog(dbContext);
        var secondPage = await catalog.GetPageAsync(
            new VacancyListQuery(null, null, null, 2, 2));
        var emptyPage = await catalog.GetPageAsync(
            new VacancyListQuery(null, null, null, 3, 2));

        Assert.Equal(3, secondPage.TotalCount);
        Assert.Equal(2, secondPage.TotalPages);
        Assert.Equal("First", Assert.Single(secondPage.Items).Title);
        Assert.Empty(emptyPage.Items);
        Assert.Equal(3, emptyPage.TotalCount);
        Assert.Equal(2, emptyPage.TotalPages);
    }

    [Fact]
    public async Task GetPageAsync_ReturnsEmptyResultWhenNothingMatches()
    {
        await using var dbContext = CreateDbContext();
        var company = CreateCompany();
        dbContext.Companies.Add(company);
        dbContext.Vacancies.Add(
            CreateVacancy(company, "Backend-разработчик", VacancyStatus.Published, 1));
        await dbContext.SaveChangesAsync();

        var result = await new VacancyCatalog(dbContext).GetPageAsync(
            new VacancyListQuery(null, "Несуществующий город", null, 1, 20));

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);
    }

    [Fact]
    public async Task GetPageAsync_ReturnsEmptyPageWhenOffsetExceedsIntegerRange()
    {
        await using var dbContext = CreateDbContext();
        var company = CreateCompany();
        dbContext.Companies.Add(company);
        dbContext.Vacancies.Add(
            CreateVacancy(company, "Backend-разработчик", VacancyStatus.Published, 1));
        await dbContext.SaveChangesAsync();

        var result = await new VacancyCatalog(dbContext).GetPageAsync(
            new VacancyListQuery(null, null, null, int.MaxValue, 100));

        Assert.Empty(result.Items);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(1, result.TotalPages);
    }

    private static JobMatchDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<JobMatchDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new TestJobMatchDbContext(options);
    }

    private static Company CreateCompany() =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = "Тестовая компания",
            Description = "Только вымышленные данные.",
            City = "Москва"
        };

    private static Vacancy CreateVacancy(
        Company company,
        string title,
        VacancyStatus status,
        int? publishedDay,
        string city = "Москва",
        WorkFormat workFormat = WorkFormat.Hybrid) =>
        new()
        {
            Id = Guid.NewGuid(),
            Company = company,
            CompanyId = company.Id,
            Title = title,
            Description = "Описание",
            Requirements = "Требования",
            City = city,
            WorkFormat = workFormat,
            Status = status,
            PublishedAt = publishedDay is null
                ? null
                : new DateTimeOffset(2026, 9, publishedDay.Value, 9, 0, 0, TimeSpan.Zero)
        };
}
