using System.Data.Common;
using AutoLot.Api.Auth;
using AutoLot.Api.Extensions;
using AutoLot.Api.Filters;
using AutoLot.Application;
using AutoLot.Application.Common.Abstractions;
using AutoLot.Infrastructure;
using AutoLot.Infrastructure.Identity;
using AutoLot.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Scalar.AspNetCore;

const string CorsPolicy = "autolot-frontend";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddAutoLotAuthentication(builder.Configuration);
builder.Services.AddAutoLotRateLimiting();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

builder.Services.AddControllers(options => options.Filters.Add<FluentValidationFilter>());
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

// Походження фронтенду — з конфігурації, білим списком (SPEC §8).
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy(CorsPolicy, policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy("API відповідає"), tags: ["live"])
    .AddNpgSql(
        DatabaseConnection.Resolve(builder.Configuration),
        name: "postgres",
        tags: ["ready"]);

var app = builder.Build();

await SeedIdentityAsync(app);

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}
else
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseCors(CorsPolicy);
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = HealthCheckResponseWriter.WriteAsync,
});

// Для оркестратора: live — «процес живий», ready — «залежності на місці».
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live"),
    ResponseWriter = HealthCheckResponseWriter.WriteAsync,
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = HealthCheckResponseWriter.WriteAsync,
});

await app.RunAsync();

/// <summary>
/// Ролі та перший адміністратор мають існувати до першого запиту. Якщо база
/// недоступна чи не мігрована, застосунок усе одно піднімаємо: про це чесно
/// розкаже /health, а падіння на старті сховало б причину.
/// </summary>
static async Task SeedIdentityAsync(WebApplication app)
{
    await using var scope = app.Services.CreateAsyncScope();

    var seeder = scope.ServiceProvider.GetRequiredService<IdentitySeeder>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        await seeder.SeedAsync();
    }
    catch (DbException exception)
    {
        logger.LogError(
            exception,
            "Сід ролей і адміністратора не виконано — база недоступна або не мігрована");
    }
}

/// <summary>Видимий для інтеграційних тестів через WebApplicationFactory.</summary>
public partial class Program;
