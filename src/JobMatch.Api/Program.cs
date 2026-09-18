var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var app = builder.Build();

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

app.Run();