using JobMatch.Application.Vacancies;
using JobMatch.Domain;
using JobMatch.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JobMatch.Infrastructure.Vacancies;

public sealed class VacancyCatalog(JobMatchDbContext dbContext) : IVacancyCatalog
{
    public async Task<VacancyPageDto> GetPageAsync(
        VacancyListQuery query,
        CancellationToken cancellationToken = default)
    {
        var vacancies = dbContext.Vacancies
            .AsNoTracking()
            .Where(vacancy =>
                vacancy.Status == VacancyStatus.Published &&
                vacancy.PublishedAt != null);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            vacancies = vacancies.Where(vacancy =>
                vacancy.Title.ToLower().Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(query.City))
        {
            var city = query.City.Trim().ToLower();
            vacancies = vacancies.Where(vacancy =>
                vacancy.City.ToLower() == city);
        }

        if (query.WorkFormat is not null)
        {
            vacancies = vacancies.Where(vacancy =>
                vacancy.WorkFormat == query.WorkFormat);
        }

        var totalCount = await vacancies.CountAsync(cancellationToken);
        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)query.PageSize);

        var offset = ((long)query.Page - 1) * query.PageSize;

        if (offset > int.MaxValue)
        {
            return new VacancyPageDto(
                [],
                query.Page,
                query.PageSize,
                totalCount,
                totalPages);
        }

        var items = await vacancies
            .OrderByDescending(vacancy => vacancy.PublishedAt)
            .ThenBy(vacancy => vacancy.Id)
            .Skip((int)offset)
            .Take(query.PageSize)
            .Select(vacancy => new VacancySummaryDto(
                vacancy.Id,
                vacancy.Title,
                vacancy.City,
                vacancy.WorkFormat,
                vacancy.SalaryFrom,
                vacancy.SalaryTo,
                new CompanySummaryDto(
                    vacancy.Company.Id,
                    vacancy.Company.Name),
                vacancy.PublishedAt!.Value))
            .ToListAsync(cancellationToken);

        return new VacancyPageDto(
            items,
            query.Page,
            query.PageSize,
            totalCount,
            totalPages);
    }
}
