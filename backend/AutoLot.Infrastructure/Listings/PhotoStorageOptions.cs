namespace AutoLot.Infrastructure.Listings;

public sealed class PhotoStorageOptions
{
    public const string SectionName = "PhotoStorage";

    /// <summary>
    /// Корінь сховища. Відносний шлях рахується від теки застосунку; у
    /// репозиторій ця тека не потрапляє (див. .gitignore).
    /// </summary>
    public string RootPath { get; set; } = "uploads";

    /// <summary>Максимальний розмір завантаження, байт. За замовчуванням 10 МБ.</summary>
    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>Скільки фото дозволено одному оголошенню.</summary>
    public int MaxPhotosPerListing { get; set; } = 20;
}
