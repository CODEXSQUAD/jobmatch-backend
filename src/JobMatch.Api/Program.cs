using JobMatch.Api.Vacancies;
using JobMatch.Infrastructure;
using JobMatch.Infrastructure.Persistence;
using JobMatch.Infrastructure.Seeding;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
var seedDemoData = args.Contains(
    "--seed-demo-data",
    StringComparer.OrdinalIgnoreCase);

if (seedDemoData && !builder.Environment.IsDevelopment())
{
    Console.Error.WriteLine(
        "Демонстрационные данные можно добавлять только в окружении Development.");
    Environment.ExitCode = 1;
    return;
}

builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (seedDemoData)
{
    await using var scope = app.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<JobMatchDbContext>();

    await dbContext.Database.MigrateAsync();
    await DemoDataSeeder.SeedAsync(dbContext);
    return;
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint(
            "/openapi/v1.json",
            "JobMatch API v1");
    });
}

app.MapGet("/health", () =>
    Results.Ok(new { status = "ok" }))
    .WithName("Health")
    .WithSummary("Проверка работоспособности API");

app.MapVacancyEndpoints();

app.Run();

public partial class Program;
