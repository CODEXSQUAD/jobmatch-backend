using JobMatch.Application.Vacancies;
using JobMatch.Domain;
using Microsoft.OpenApi;
using System.Globalization;
using System.Text.Json.Nodes;

namespace JobMatch.Api.Vacancies;

public static class VacancyEndpoints
{
    public static IEndpointRouteBuilder MapVacancyEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/api/vacancies",
                GetVacanciesAsync)
            .WithName("listVacancies")
            .WithTags("Vacancies")
            .WithSummary("Получить каталог опубликованных вакансий")
            .WithDescription(
                "Поддерживаются поиск по названию q, фильтры city и workFormat, " +
                "а также пагинация page/pageSize.")
            .Produces<VacancyPageDto>()
            .Produces<ApiErrorDto>(StatusCodes.Status400BadRequest)
            .AddOpenApiOperationTransformer((operation, _, _) =>
            {
                ConfigureOpenApi(operation);
                return Task.CompletedTask;
            });

        return endpoints;
    }

    private static async Task<IResult> GetVacanciesAsync(
        IVacancyCatalog catalog,
        CancellationToken cancellationToken,
        HttpRequest request)
    {
        var validationError = TryCreateQuery(
            request.Query,
            out var query);

        if (validationError is not null)
        {
            return Results.BadRequest(new ApiErrorDto(
                "invalid_query",
                validationError));
        }

        var result = await catalog.GetPageAsync(
            query!,
            cancellationToken);

        return Results.Ok(result);
    }

    private static string? TryCreateQuery(
        IQueryCollection parameters,
        out VacancyListQuery? query)
    {
        query = null;

        var q = GetValue(parameters, "q");
        var city = GetValue(parameters, "city");
        var workFormat = GetValue(parameters, "workFormat");

        if (!TryParsePositiveInteger(parameters, "page", 1, out var page))
        {
            return "page должен быть целым числом не меньше 1.";
        }

        if (!TryParsePositiveInteger(parameters, "pageSize", 20, out var pageSize) ||
            pageSize > 100)
        {
            return "pageSize должен быть целым числом от 1 до 100.";
        }

        if (q is not null && (string.IsNullOrWhiteSpace(q) || q.Trim().Length > 200))
        {
            return "q должен содержать от 1 до 200 символов.";
        }

        if (city is not null && (string.IsNullOrWhiteSpace(city) || city.Trim().Length > 120))
        {
            return "city должен содержать от 1 до 120 символов.";
        }

        WorkFormat? parsedWorkFormat = null;

        if (workFormat is not null)
        {
            var matchingName = Enum.GetNames<WorkFormat>()
                .FirstOrDefault(name => string.Equals(
                    name,
                    workFormat,
                    StringComparison.OrdinalIgnoreCase));

            if (matchingName is null)
            {
                return "workFormat должен быть Office, Remote или Hybrid.";
            }

            parsedWorkFormat = Enum.Parse<WorkFormat>(matchingName);
        }

        query = new VacancyListQuery(
            q,
            city,
            parsedWorkFormat,
            page,
            pageSize);

        return null;
    }

    private static string? GetValue(
        IQueryCollection parameters,
        string name)
    {
        return parameters.TryGetValue(name, out var values)
            ? values.ToString()
            : null;
    }

    private static bool TryParsePositiveInteger(
        IQueryCollection parameters,
        string name,
        int defaultValue,
        out int value)
    {
        if (!parameters.TryGetValue(name, out var rawValue))
        {
            value = defaultValue;
            return true;
        }

        return int.TryParse(
                   rawValue.ToString(),
                   NumberStyles.None,
                   CultureInfo.InvariantCulture,
                   out value) &&
               value >= 1;
    }

    private static void ConfigureOpenApi(OpenApiOperation operation)
    {
        operation.OperationId = "listVacancies";
        operation.Parameters =
        [
            StringParameter(
                "q",
                "Поиск по названию вакансии без учёта регистра.",
                maxLength: 200),
            StringParameter(
                "city",
                "Фильтр по городу без учёта регистра.",
                maxLength: 120),
            new OpenApiParameter
            {
                Name = "workFormat",
                In = ParameterLocation.Query,
                Description = "Формат работы.",
                Schema = new OpenApiSchema
                {
                    Type = JsonSchemaType.String,
                    Enum =
                    [
                        JsonValue.Create("Office")!,
                        JsonValue.Create("Remote")!,
                        JsonValue.Create("Hybrid")!
                    ]
                }
            },
            IntegerParameter(
                "page",
                "Номер страницы, начиная с 1.",
                defaultValue: 1),
            IntegerParameter(
                "pageSize",
                "Число вакансий на странице.",
                defaultValue: 20,
                maximum: 100)
        ];

    }

    private static OpenApiParameter StringParameter(
        string name,
        string description,
        int maxLength)
    {
        return new OpenApiParameter
        {
            Name = name,
            In = ParameterLocation.Query,
            Description = description,
            Schema = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                MinLength = 1,
                MaxLength = maxLength
            }
        };
    }

    private static OpenApiParameter IntegerParameter(
        string name,
        string description,
        int defaultValue,
        int? maximum = null)
    {
        return new OpenApiParameter
        {
            Name = name,
            In = ParameterLocation.Query,
            Description = description,
            Schema = new OpenApiSchema
            {
                Type = JsonSchemaType.Integer,
                Format = "int32",
                Minimum = "1",
                Maximum = maximum?.ToString(CultureInfo.InvariantCulture),
                Default = JsonValue.Create(defaultValue)
            }
        };
    }

    public sealed record ApiErrorDto(
        string Code,
        string Message);
}
