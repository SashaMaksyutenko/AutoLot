using AutoLot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AutoLot.Api.Persistence;

/// <summary>
/// Використовується лише інструментом dotnet-ef: він піднімає контекст без
/// веб-хоста, тож рядок підключення читаємо з тих самих user-secrets і змінних
/// оточення, що й застосунок, незалежно від ASPNETCORE_ENVIRONMENT.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AutoLotDbContext>
{
    public AutoLotDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<DesignTimeDbContextFactory>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var options = new DbContextOptionsBuilder<AutoLotDbContext>()
            .UseNpgsql(
                DatabaseConnection.Resolve(configuration),
                npgsql => npgsql.MigrationsAssembly(typeof(AutoLotDbContext).Assembly.GetName().Name))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new AutoLotDbContext(options);
    }
}
