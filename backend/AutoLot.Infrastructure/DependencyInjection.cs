using AutoLot.Application.Auth;
using AutoLot.Application.Cars;
using AutoLot.Application.Catalog;
using AutoLot.Application.Common.Abstractions;
using AutoLot.Application.Geo;
using AutoLot.Application.Listings;
using AutoLot.Application.Users;
using AutoLot.Domain.Identity;
using AutoLot.Infrastructure.Cars;
using AutoLot.Infrastructure.Catalog;
using AutoLot.Infrastructure.Geo;
using AutoLot.Infrastructure.Identity;
using AutoLot.Infrastructure.Listings;
using AutoLot.Infrastructure.Persistence;
using AutoLot.Infrastructure.Persistence.Interceptors;
using AutoLot.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AutoLot.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

        services.AddPersistence(configuration);
        services.AddIdentity(configuration);
        services.AddGeography();
        services.AddCarReference();
        services.AddListings(configuration);

        return services;
    }

    private static IServiceCollection AddListings(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ExchangeRateOptions>(configuration.GetSection(ExchangeRateOptions.SectionName));

        services.Configure<PhotoStorageOptions>(configuration.GetSection(PhotoStorageOptions.SectionName));
        services.Configure<DemoDataOptions>(configuration.GetSection(DemoDataOptions.SectionName));

        services.AddScoped<IExchangeRateProvider, ConfiguredExchangeRateProvider>();
        services.AddScoped<IPhotoStorage, LocalPhotoStorage>();
        services.AddScoped<IListingPhotoService, ListingPhotoService>();
        services.AddScoped<ICatalogService, CatalogService>();
        services.AddScoped<IDataSeeder, DemoDataSeeder>();
        services.AddScoped<ListingMapper>();
        services.AddScoped<IListingService, ListingService>();
        services.AddScoped<IModerationService, ModerationService>();

        return services;
    }

    private static IServiceCollection AddGeography(this IServiceCollection services)
    {
        services.AddScoped<IGeoCatalog, GeoCatalog>();
        services.AddScoped<IDataSeeder, GeographySeeder>();

        return services;
    }

    private static IServiceCollection AddCarReference(this IServiceCollection services)
    {
        services.AddScoped<ICarCatalog, CarCatalog>();
        services.AddScoped<IDataSeeder, CarReferenceSeeder>();

        return services;
    }

    private static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = DatabaseConnection.Resolve(configuration);

        services.AddScoped<AuditableEntityInterceptor>();

        services.AddDbContext<AutoLotDbContext>((serviceProvider, options) =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(AutoLotDbContext).Assembly.GetName().Name);

                // EnableRetryOnFailure свідомо не вмикаємо: ставки йдуть у явній
                // транзакції з блокуванням рядка (SPEC §5), а стратегія повторів
                // мовчки таку транзакцію не переграє. Повтори — на рівні сценарію.
            });

            // PostgreSQL приємніше читати в snake_case, ніж у лапках з PascalCase.
            options.UseSnakeCaseNamingConvention();

            options.AddInterceptors(serviceProvider.GetRequiredService<AuditableEntityInterceptor>());
        });

        return services;
    }

    private static IServiceCollection AddIdentity(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations();

        services.Configure<AdminSeedOptions>(configuration.GetSection(AdminSeedOptions.SectionName));

        // AddIdentityCore, а не AddIdentity: cookie-сесії нам не потрібні,
        // автентифікація йде через JWT, тож SignInManager лишається зайвим.
        services.AddIdentityCore<User>(options =>
            {
                options.User.RequireUniqueEmail = true;

                // Політика має збігатися з RegisterRequestValidator.
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;

                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);

                // Підтвердження email вмикаємо разом із поштовою інфраструктурою,
                // яка за планом іде пізніше.
                options.SignIn.RequireConfirmedEmail = false;
            })
            .AddRoles<Role>()
            .AddEntityFrameworkStores<AutoLotDbContext>();

        // Провайдери токенів (AddDefaultTokenProviders) поки не підключаємо:
        // вони потрібні для підтвердження пошти та скидання пароля, а це
        // приходить разом із поштовою інфраструктурою.

        services.AddScoped<JwtTokenGenerator>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserProfileService, UserProfileService>();
        services.AddScoped<IDataSeeder, IdentitySeeder>();

        return services;
    }
}
