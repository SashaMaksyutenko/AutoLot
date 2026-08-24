namespace AutoLot.Infrastructure.Listings;

/// <summary>
/// Налаштування демонстраційних даних. Вимкнені за замовчуванням: вигадані
/// оголошення потрібні лише під час розробки й показу.
/// </summary>
public sealed class DemoDataOptions
{
    public const string SectionName = "DemoData";

    public bool Enabled { get; set; }

    public int ListingCount { get; set; } = 200;

    public int SellerCount { get; set; } = 12;

    /// <summary>Одне зерно на всі випадкові значення — набір відтворюваний.</summary>
    public int Seed { get; set; } = 20260823;

    /// <summary>Пароль демо-продавців. Реальних людей за цими акаунтами немає.</summary>
    public string SellerPassword { get; set; } = "Demo-2026x";
}
