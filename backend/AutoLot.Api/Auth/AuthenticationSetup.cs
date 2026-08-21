using System.Security.Claims;
using System.Text;
using System.Text.Json;
using AutoLot.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace AutoLot.Api.Auth;

public static class AuthenticationSetup
{
    /// <summary>
    /// Проміжна cookie, у якій лежить відповідь Google рівно між редіректом
    /// назад і видачею наших токенів. Живе кілька хвилин.
    /// </summary>
    public const string ExternalScheme = "AutoLot.External";

    public const string GoogleProvider = GoogleDefaults.AuthenticationScheme;

    public static IServiceCollection AddAutoLotAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException(
                $"Секція конфігурації '{JwtOptions.SectionName}' відсутня.");

        if (string.IsNullOrWhiteSpace(jwt.Key) || jwt.Key.Length < 32)
        {
            throw new InvalidOperationException(
                "Jwt:Key не налаштований або закороткий (потрібно щонайменше 32 символи). " +
                "Задайте його командою: dotnet user-secrets set \"Jwt:Key\" \"<ключ>\".");
        }

        var authentication = services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme);

        authentication.AddJwtBearer(options =>
        {
            // Без мапінгу вхідних клеймів: у токені короткі імена (sub, role),
            // і ми не хочемо, щоб ASP.NET перетворював їх на довгі URI.
            options.MapInboundClaims = false;

            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwt.Issuer,
                ValidateAudience = true,
                ValidAudience = jwt.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                ValidateLifetime = true,

                // Годинник сервера — джерело істини (SPEC §5), тож запас мінімальний.
                ClockSkew = TimeSpan.FromSeconds(30),

                NameClaimType = AutoLotClaims.Name,
                RoleClaimType = AutoLotClaims.Role,
            };
        });

        var googleClientId = configuration["Authentication:Google:ClientId"];
        var googleClientSecret = configuration["Authentication:Google:ClientSecret"];

        // Ключі Google створює власник проєкту; поки їх немає, схему не
        // реєструємо взагалі — інакше застосунок не підніметься.
        if (!string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret))
        {
            authentication.AddCookie(ExternalScheme, options =>
            {
                options.Cookie.Name = "autolot.external";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.ExpireTimeSpan = TimeSpan.FromMinutes(5);
                options.SlidingExpiration = false;
            });

            authentication.AddGoogle(options =>
            {
                options.ClientId = googleClientId;
                options.ClientSecret = googleClientSecret;
                options.SignInScheme = ExternalScheme;
                options.CallbackPath = "/signin-google";
                options.SaveTokens = false;

                // Google віддає email_verified, але типовий набір клеймів його
                // не містить, а нам без нього не можна прив'язувати акаунти.
                options.Events.OnCreatingTicket = context =>
                {
                    if (context.User.TryGetProperty("email_verified", out var verified))
                    {
                        context.Identity?.AddClaim(new Claim(
                            "email_verified",
                            verified.ValueKind == JsonValueKind.True ? "true" : "false"));
                    }

                    return Task.CompletedTask;
                };
            });
        }

        services.AddAuthorization();

        return services;
    }

    public static bool IsGoogleConfigured(IConfiguration configuration) =>
        !string.IsNullOrWhiteSpace(configuration["Authentication:Google:ClientId"])
        && !string.IsNullOrWhiteSpace(configuration["Authentication:Google:ClientSecret"]);
}
