using System.Data.Common;
using System.Text.Json.Serialization;
using AutoLot.Api.Auth;
using AutoLot.Api.Extensions;
using AutoLot.Api.Filters;
using AutoLot.Api.Localization;
using AutoLot.Application;
using AutoLot.Application.Common.Abstractions;
using AutoLot.Infrastructure;
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

builder.Services.AddAutoLotLocalization();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

builder.Services
    .AddControllers(options => options.Filters.Add<FluentValidationFilter>())
    .AddJsonOptions(options =>
    {
        // Перелічення віддаємо назвами, а не числами. По-перше, клієнт і так
        // отримує назви з /api/cars/attributes — інакше він не зміг би
        // зіставити «Petrol» зі списку з «1» в оголошенні. По-друге, у
        // query-рядку вони теж приймаються назвами, тож напрямки збігаються.
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
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

await SeedDatabaseAsync(app);

app.UseExceptionHandler();
app.UseStatusCodePages();

// Має стояти раніше за контролери: саме тут з Accept-Language обирається
// мова, якою довідники віддадуть свої назви.
app.UseRequestLocalization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}
else
{
    app.UseHsts();

    // Перенаправлення на HTTPS лише поза розробкою. У профілі "http" немає
    // HTTPS-порту, тож ASP.NET не знав би, куди перенаправляти, і на кожному
    // запуску попереджав про це. До того ж у розробці фронтенд ходить через
    // проксі Vite звичайним HTTP — перенаправляти нікуди й не треба.
    app.UseHttpsRedirection();
}
app.UseCors(CorsPolicy);
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.UseListingPhotos();

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
/// Довідники, ролі та перший адміністратор мають існувати до першого запиту.
/// Якщо база недоступна чи не мігрована, застосунок усе одно піднімаємо: про
/// це чесно розкаже /health, а падіння на старті сховало б причину.
/// </summary>
static async Task SeedDatabaseAsync(WebApplication app)
{
    await using var scope = app.Services.CreateAsyncScope();

    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    // Порядок задають самі сідери — тому, хто додає новий, не треба лізти сюди.
    var seeders = scope.ServiceProvider
        .GetServices<IDataSeeder>()
        .OrderBy(seeder => seeder.Order);

    foreach (var seeder in seeders)
    {
        try
        {
            await seeder.SeedAsync();
        }
        catch (DbException exception)
        {
            logger.LogError(
                exception,
                "Сід {Seeder} не виконано — база недоступна або не мігрована",
                seeder.GetType().Name);
        }
    }
}

/// <summary>Видимий для інтеграційних тестів через WebApplicationFactory.</summary>
public partial class Program;
