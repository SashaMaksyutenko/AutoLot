using System.Text.Json;

namespace AutoLot.Infrastructure.Persistence;

/// <summary>
/// Читає вбудований сід-файл.
///
/// Довідники їдуть усередині збірки (див. EmbeddedResource у .csproj), а не
/// поруч із застосунком: інакше сід залежав би від того, чи скопіювали файли
/// при публікації, і падав би вже на робочому сервері.
/// </summary>
internal static class SeedResource
{
    /// <summary>
    /// Імена властивостей у файлах написані з малої літери, у типах — з
    /// великої. Без цієї поблажки довелося б розставляти [JsonPropertyName]
    /// над кожним полем кожного сід-типу.
    /// </summary>
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Розбирає файл у вказаний тип. Відсутній ресурс — це помилка збірки,
    /// а не даних, тож падаємо одразу й зрозуміло: мовчазний порожній
    /// довідник шукали б потім годинами.
    /// </summary>
    public static async Task<TDocument> ReadAsync<TDocument>(
        string resourceName,
        CancellationToken cancellationToken = default)
        where TDocument : new()
    {
        // Ресурси шукаємо у збірці, де живе цей клас, — тій самій, куди
        // вбудовано всі сід-файли.
        await using var stream = typeof(SeedResource).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Вбудований ресурс '{resourceName}' не знайдено. Перевірте, що файл додано як EmbeddedResource.");

        return await JsonSerializer.DeserializeAsync<TDocument>(stream, SerializerOptions, cancellationToken)
            ?? new TDocument();
    }
}
