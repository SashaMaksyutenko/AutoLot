using AutoLot.Application.Admin;
using AutoLot.Application.Auctions;
using AutoLot.Application.Billing;
using AutoLot.Application.Search;
using AutoLot.Application.Auth;
using AutoLot.Application.Cars;
using AutoLot.Application.Catalog;
using AutoLot.Application.Chat;
using AutoLot.Application.Common.Abstractions;
using AutoLot.Application.Dealers;
using AutoLot.Application.Favorites;
using AutoLot.Application.Geo;
using AutoLot.Application.Listings;
using AutoLot.Application.Users;
using AutoLot.Domain.Identity;
using AutoLot.Infrastructure.Admin;
using AutoLot.Infrastructure.Auctions;
using AutoLot.Infrastructure.Cars;
using AutoLot.Infrastructure.Catalog;
using AutoLot.Infrastructure.Email;
using AutoLot.Infrastructure.Billing;
using AutoLot.Infrastructure.Chat;
using AutoLot.Infrastructure.Search;
using AutoLot.Infrastructure.Dealers;
using AutoLot.Infrastructure.Favorites;
using AutoLot.Infrastructure.Geo;
using AutoLot.Infrastructure.Identity;
using AutoLot.Infrastructure.Listings;
using AutoLot.Infrastructure.Persistence;
using AutoLot.Infrastructure.Persistence.Interceptors;
using AutoLot.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace AutoLot.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

        services.AddEmail(configuration);
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
        services.AddScoped<ISavedSearchService, SavedSearchService>();
        services.AddScoped<IDataSeeder, DemoDataSeeder>();
        services.AddScoped<ListingMapper>();
        services.AddScoped<ListingAccess>();
        services.AddScoped<IListingService, ListingService>();
        services.AddScoped<IModerationService, ModerationService>();
        services.AddScoped<IListingQuestionService, ListingQuestionService>();
        services.AddScoped<IListingReportService, ListingReportService>();
        services.AddScoped<IReviewService, ReviewService>();
        services.AddScoped<BillingService>();
        services.AddScoped<IBillingService>(provider => provider.GetRequiredService<BillingService>());
        services.AddScoped<IListingAllowance>(provider => provider.GetRequiredService<BillingService>());
        services.AddScoped<IDataSeeder, PlanSeeder>();
        services.AddScoped<IDataSeeder, ModerationSeeder>();
        services.AddScoped<IChatService, ChatService>();
        services.AddScoped<IDealershipService, DealershipService>();
        services.AddScoped<IFavoriteService, FavoriteService>();
        services.AddScoped<IAuctionService, AuctionService>();
        services.AddScoped<IAuctionCloser, AuctionCloser>();

        services.AddAuctionScheduling();

        return services;
    }

    /// <summary>
    /// Планувальник закриття торгів (SPEC §5).
    ///
    /// Сховище розкладу — у пам'яті, і це свідомо: єдине, що там лежить, —
    /// «закрити лот N о такій-то годині», а цю інформацію завжди можна
    /// відновити з бази. Саме це й робить <see cref="AuctionScheduleRecovery"/>
    /// на старті, тож окрема база розкладу лише додала б таблиць і шансів
    /// розійтися з правдою.
    /// </summary>
    private static IServiceCollection AddAuctionScheduling(this IServiceCollection services)
    {
        services.AddQuartz(quartz => quartz.UseInMemoryStore());

        services.AddQuartzHostedService(options =>
        {
            // Не гасити застосунок, поки задача не доробила: обірване посеред
            // транзакції закриття лишило б торги в невизначеному стані.
            options.WaitForJobsToComplete = true;
        });

        services.AddScoped<IAuctionScheduler, QuartzAuctionScheduler>();
        services.AddHostedService<AuctionScheduleRecovery>();

        return services;
    }

    /// <summary>
    /// Пошта. Один відправник на обидва режими: у розробці листи лягають
    /// файлами в теку, у продакшені йдуть через SMTP — див. EmailOptions.
    /// </summary>
    private static IServiceCollection AddEmail(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<EmailOptions>()
            .Bind(configuration.GetSection(EmailOptions.SectionName))
            .ValidateDataAnnotations();

        services.AddScoped<AccountEmails>();
        services.AddScoped<IEmailSender, SmtpEmailSender>();

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

        // Провайдери токенів для відновлення пароля й підтвердження пошти
        // реєструються в шарі Api — див. AddAutoLotTokenProviders. Причина
        // технічна: AddDefaultTokenProviders живе у фреймворку ASP.NET Core,
        // якого тут немає й не має бути. Тягнути сюди весь вебфреймворк
        // заради одного виклику означало б відкрити Infrastructure доступ до
        // контролерів і SignalR — рівно те, чого ми уникали в пункті 7.

        services.AddScoped<JwtTokenGenerator>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAccountRecoveryService, AccountRecoveryService>();
        services.AddScoped<IUserProfileService, UserProfileService>();
        services.AddScoped<IPublicProfileService, PublicProfileService>();
        services.AddScoped<IAdminService, AdminService>();
        services.AddScoped<IDataSeeder, IdentitySeeder>();

        return services;
    }
}
