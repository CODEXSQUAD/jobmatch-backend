using JobMatch.Domain;
using JobMatch.Infrastructure.Identity;
using JobMatch.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace JobMatch.Api.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api").WithTags("Auth");

        group.MapPost("/auth/register", RegisterAsync)
            .WithName("register")
            .Produces<CurrentUserResponse>(StatusCodes.Status201Created)
            .Produces<ApiError>(StatusCodes.Status400BadRequest)
            .Produces<ApiError>(StatusCodes.Status409Conflict);

        group.MapPost("/auth/login", LoginAsync)
            .WithName("login")
            .Produces<CurrentUserResponse>()
            .Produces<ApiError>(StatusCodes.Status400BadRequest)
            .Produces<ApiError>(StatusCodes.Status401Unauthorized);

        group.MapPost("/auth/logout", LogoutAsync)
            .WithName("logout")
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent);

        group.MapGet("/users/me", CurrentUserAsync)
            .WithName("getCurrentUser")
            .RequireAuthorization()
            .Produces<CurrentUserResponse>()
            .Produces<ApiError>(StatusCodes.Status401Unauthorized);

        return endpoints;
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        JobMatchDbContext dbContext)
    {
        var email = request.Email?.Trim();
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.BadRequest(new ApiError(
                "validation_error",
                "Укажите email и пароль."));
        }

        if (!Enum.TryParse<UserRole>(request.Role, true, out var role))
        {
            return Results.BadRequest(new ApiError(
                "validation_error",
                "Роль должна быть Candidate или Employer."));
        }

        if (await userManager.FindByEmailAsync(email) is not null)
        {
            return Results.Conflict(new ApiError(
                "email_already_registered",
                "Пользователь с таким email уже зарегистрирован."));
        }

        var user = new ApplicationUser
        {
            Email = email,
            UserName = email,
            Role = role,
        };

        var creation = await userManager.CreateAsync(user, request.Password);
        if (!creation.Succeeded)
        {
            var message = string.Join(" ", creation.Errors.Select(error => error.Description));
            return Results.BadRequest(new ApiError("validation_error", message));
        }

        if (role == UserRole.Candidate)
        {
            var profile = new CandidateProfile { UserId = user.Id };
            profile.CreditAccount = new CreditAccount
            {
                PeriodDate = DateOnly.FromDateTime(DateTime.UtcNow),
                Balance = 5,
                DailyLimit = 5,
            };
            dbContext.CandidateProfiles.Add(profile);
            await dbContext.SaveChangesAsync();
        }

        await signInManager.SignInAsync(user, isPersistent: false);
        return Results.Created("/api/users/me", ToResponse(user));
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager)
    {
        var email = request.Email?.Trim();
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.BadRequest(new ApiError(
                "validation_error",
                "Укажите email и пароль."));
        }

        var user = await userManager.FindByEmailAsync(email);
        if (user is null || !user.IsActive ||
            !await userManager.CheckPasswordAsync(user, request.Password))
        {
            return Results.Json(
                new ApiError("invalid_credentials", "Неверный email или пароль."),
                statusCode: StatusCodes.Status401Unauthorized);
        }

        await signInManager.SignInAsync(user, isPersistent: false);
        return Results.Ok(ToResponse(user));
    }

    private static async Task<IResult> LogoutAsync(SignInManager<ApplicationUser> signInManager)
    {
        await signInManager.SignOutAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> CurrentUserAsync(
        HttpContext context,
        UserManager<ApplicationUser> userManager)
    {
        var user = await userManager.GetUserAsync(context.User);
        return user is null
            ? Results.Json(
                new ApiError("unauthorized", "Требуется вход."),
                statusCode: StatusCodes.Status401Unauthorized)
            : Results.Ok(ToResponse(user));
    }

    private static CurrentUserResponse ToResponse(ApplicationUser user) =>
        new(user.Id, user.Email!, user.Role.ToString());
}

public sealed record RegisterRequest(string? Email, string? Password, string? Role);
public sealed record LoginRequest(string? Email, string? Password);
public sealed record CurrentUserResponse(Guid Id, string Email, string Role);
public sealed record ApiError(string Code, string Message);
