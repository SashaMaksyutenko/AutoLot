using AutoLot.Application.Common.Abstractions;
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
        var connectionString = DatabaseConnection.Resolve(configuration);

        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
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
}
