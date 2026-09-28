using JobMatch.Infrastructure.Persistence;
using JobMatch.Infrastructure.Seeding;
using JobMatch.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobMatch.Infrastructure.Tests;

public sealed class DemoDataSeederTests
{
    [Fact]
    public async Task SeedAsync_CanRunTwiceWithoutDuplicates()
    {
        var options = new DbContextOptionsBuilder<JobMatchDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using (var firstDbContext = new TestJobMatchDbContext(options))
        {
            await DemoDataSeeder.SeedAsync(firstDbContext);
        }

        await using var dbContext = new TestJobMatchDbContext(options);
        await DemoDataSeeder.SeedAsync(dbContext);

        Assert.Equal(3, await dbContext.Users.CountAsync());
        Assert.Equal(3, await dbContext.Companies.CountAsync());
        Assert.Equal(3, await dbContext.CompanyMembers.CountAsync());
        Assert.Equal(8, await dbContext.Vacancies.CountAsync());
        Assert.Equal(6, await dbContext.Skills.CountAsync());
        Assert.Equal(10, await dbContext.VacancySkills.CountAsync());
        Assert.Equal(6, await dbContext.Vacancies.CountAsync(
            vacancy => vacancy.Status == VacancyStatus.Published));
        Assert.Single(await dbContext.Vacancies.Where(
            vacancy => vacancy.Status == VacancyStatus.Draft).ToListAsync());
        Assert.Single(await dbContext.Vacancies.Where(
            vacancy => vacancy.Status == VacancyStatus.Closed).ToListAsync());
        Assert.Equal(3, await dbContext.Vacancies
            .Where(vacancy => vacancy.Status == VacancyStatus.Published)
            .Select(vacancy => vacancy.WorkFormat)
            .Distinct()
            .CountAsync());
        Assert.Equal(5, await dbContext.Vacancies
            .Select(vacancy => vacancy.City)
            .Distinct()
            .CountAsync());
    }
}
