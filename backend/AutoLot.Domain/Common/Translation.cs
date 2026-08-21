namespace AutoLot.Domain.Common;

/// <summary>
/// Спільна частина будь-якого перекладу довідника. Кожен рядок — одна назва
/// однією мовою; сама сутність (область, місто) назви не зберігає взагалі,
/// тому додати третю мову можна без зміни її таблиці.
/// </summary>
public abstract class Translation : Entity
{
    /// <summary>Код мови з <see cref="LanguageCodes"/>.</summary>
    public string Language { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
}
