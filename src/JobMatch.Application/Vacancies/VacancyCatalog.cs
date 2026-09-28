using JobMatch.Domain;

namespace JobMatch.Application.Vacancies;

public sealed record VacancyListQuery(
    string? Search,
    string? City,
    WorkFormat? WorkFormat,
    int Page,
    int PageSize);

public sealed record CompanySummaryDto(
    Guid Id,
    string Name);

public sealed record VacancySummaryDto(
    Guid Id,
    string Title,
    string City,
    WorkFormat WorkFormat,
    int? SalaryFrom,
    int? SalaryTo,
    CompanySummaryDto Company,
    DateTimeOffset PublishedAt);

public sealed record VacancyPageDto(
    IReadOnlyList<VacancySummaryDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

public interface IVacancyCatalog
{
    Task<VacancyPageDto> GetPageAsync(
        VacancyListQuery query,
        CancellationToken cancellationToken = default);
}
