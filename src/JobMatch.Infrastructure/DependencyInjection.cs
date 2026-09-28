using JobMatch.Application.Vacancies;
using JobMatch.Infrastructure.Identity;
using JobMatch.Infrastructure.Persistence;
using JobMatch.Infrastructure.Vacancies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JobMatch.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'DefaultConnection' is not configured.");
        }

        services.AddDbContext<JobMatchDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IVacancyCatalog, VacancyCatalog>();

        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<JobMatchDbContext>();

        return services;
    }
}
