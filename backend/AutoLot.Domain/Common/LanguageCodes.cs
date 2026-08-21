namespace AutoLot.Domain.Common;

/// <summary>
/// Мови інтерфейсу. Коди дволітерні за ISO 639-1 — саме в такому вигляді їх
/// надсилає браузер у заголовку Accept-Language, тож нічого перетворювати не треба.
/// </summary>
public static class LanguageCodes
{
    public const string Ukrainian = "uk";

    public const string English = "en";

    /// <summary>Мова, якою відповідаємо, якщо клієнт не попросив іншої.</summary>
    public const string Default = Ukrainian;

    public static IReadOnlyList<string> Supported { get; } = [Ukrainian, English];

    public static bool IsSupported(string? code) =>
        code is not null
        && Supported.Contains(code, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Зводить будь-що прислане клієнтом до підтримуваного коду: "uk-UA" стає
    /// "uk", незнайома мова — типовою.
    /// </summary>
    public static string Normalize(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return Default;
        }

        var twoLetters = code.Split('-')[0].ToLowerInvariant();

        return IsSupported(twoLetters) ? twoLetters : Default;
    }
}
