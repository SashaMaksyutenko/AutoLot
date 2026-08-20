using Microsoft.Extensions.Configuration;

namespace AutoLot.Infrastructure.Persistence;

/// <summary>
/// Єдине місце, де застосунок дізнається рядок підключення. Сам рядок живе в
/// user-secrets або змінних оточення, у репозиторії його немає (SPEC §8).
/// </summary>
public static class DatabaseConnection
{
    public const string Name = "AutoLot";

    /// <summary>
    /// Повертає рядок підключення або падає одразу зі зрозумілим текстом —
    /// краще на старті, ніж на першому запиті до бази.
    /// </summary>
    public static string Resolve(IConfiguration configuration)
    {
        return configuration.GetConnectionString(Name)
            ?? throw new InvalidOperationException(
                $"Рядок підключення '{Name}' не налаштований. Задайте його командою: " +
                $"dotnet user-secrets set \"ConnectionStrings:{Name}\" \"<connection string>\" " +
                "у проєкті AutoLot.Api.");
    }
}
