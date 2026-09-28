using JobMatch.Application.Vacancies;
using JobMatch.Domain;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace JobMatch.Infrastructure.Tests;

public sealed class VacancyEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;
    private readonly HttpClient client;

    public VacancyEndpointTests(WebApplicationFactory<Program> factory)
    {
        this.factory = factory;
        client = factory
            .WithWebHostBuilder(builder => builder.UseEnvironment("Development"))
            .CreateClient();
    }

    [Fact]
    public async Task GetVacancies_ValidQuery_ReturnsContractResponse()
    {
        var stub = new StubVacancyCatalog();
        using var contractClient = factory
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IVacancyCatalog>();
                    services.AddSingleton<IVacancyCatalog>(stub);
                });
            })
            .CreateClient();

        var response = await contractClient.GetAsync(
            "/api/vacancies?q=backend&city=Москва&workFormat=Hybrid&page=2&pageSize=5");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("backend", stub.LastQuery?.Search);
        Assert.Equal("Москва", stub.LastQuery?.City);
        Assert.Equal(WorkFormat.Hybrid, stub.LastQuery?.WorkFormat);
        Assert.Equal(2, body.GetProperty("page").GetInt32());
        Assert.Equal(5, body.GetProperty("pageSize").GetInt32());
        Assert.Equal(1, body.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, body.GetProperty("totalPages").GetInt32());

        var item = body.GetProperty("items")[0];
        Assert.Equal("Backend-разработчик", item.GetProperty("title").GetString());
        Assert.Equal("Hybrid", item.GetProperty("workFormat").GetString());
        Assert.Equal(150_000, item.GetProperty("salaryFrom").GetInt32());
        Assert.Equal(JsonValueKind.Null, item.GetProperty("salaryTo").ValueKind);
        Assert.Equal("Тестовая компания", item.GetProperty("company").GetProperty("name").GetString());
        Assert.Equal(JsonValueKind.String, item.GetProperty("publishedAt").ValueKind);
    }

    [Theory]
    [InlineData("page=abc")]
    [InlineData("page=0")]
    [InlineData("pageSize=101")]
    [InlineData("workFormat=999")]
    [InlineData("workFormat=Office%2CRemote")]
    public async Task GetVacancies_InvalidQuery_ReturnsContractError(string query)
    {
        var response = await client.GetAsync($"/api/vacancies?{query}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid_query", body.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(
            body.GetProperty("message").GetString()));
    }

    [Fact]
    public async Task GeneratedOpenApi_DescribesVacancyQueryContract()
    {
        var document = await client.GetFromJsonAsync<JsonElement>("/openapi/v1.json");
        var operation = document
            .GetProperty("paths")
            .GetProperty("/api/vacancies")
            .GetProperty("get");

        Assert.Equal("listVacancies", operation.GetProperty("operationId").GetString());

        var parameters = operation.GetProperty("parameters")
            .EnumerateArray()
            .ToDictionary(
                parameter => parameter.GetProperty("name").GetString()!,
                parameter => parameter.GetProperty("schema"));

        Assert.Equal(1, parameters["q"].GetProperty("minLength").GetInt32());
        Assert.Equal(200, parameters["q"].GetProperty("maxLength").GetInt32());
        Assert.Equal(120, parameters["city"].GetProperty("maxLength").GetInt32());
        Assert.Equal(
            ["Office", "Remote", "Hybrid"],
            parameters["workFormat"].GetProperty("enum")
                .EnumerateArray()
                .Select(value => value.GetString()));
        Assert.Equal(1, parameters["page"].GetProperty("minimum").GetInt32());
        Assert.Equal(1, parameters["page"].GetProperty("default").GetInt32());
        Assert.Equal(100, parameters["pageSize"].GetProperty("maximum").GetInt32());
        Assert.Equal(20, parameters["pageSize"].GetProperty("default").GetInt32());
    }

    private sealed class StubVacancyCatalog : IVacancyCatalog
    {
        public VacancyListQuery? LastQuery { get; private set; }

        public Task<VacancyPageDto> GetPageAsync(
            VacancyListQuery query,
            CancellationToken cancellationToken = default)
        {
            LastQuery = query;

            var item = new VacancySummaryDto(
                new Guid("40000000-0000-4000-8000-000000000001"),
                "Backend-разработчик",
                "Москва",
                WorkFormat.Hybrid,
                150_000,
                null,
                new CompanySummaryDto(
                    new Guid("20000000-0000-4000-8000-000000000001"),
                    "Тестовая компания"),
                new DateTimeOffset(2026, 9, 20, 9, 0, 0, TimeSpan.Zero));

            return Task.FromResult(new VacancyPageDto(
                [item],
                query.Page,
                query.PageSize,
                1,
                1));
        }
    }
}
