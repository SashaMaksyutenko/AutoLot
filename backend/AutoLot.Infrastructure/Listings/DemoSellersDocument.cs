namespace AutoLot.Infrastructure.Listings;

/// <summary>
/// Опис файла demo-sellers.json — переліку вигаданих продавців і їхніх салонів.
///
/// Типи внутрішні (internal), бо поза складанням демо-даних вони нікому не
/// потрібні. Тести їх бачать адресно — через InternalsVisibleTo у .csproj:
/// вміст сід-файлів перевіряється тестами, як і в географії.
/// </summary>
internal sealed class DemoSellersDocument
{
    public List<DemoSellerRow> Sellers { get; init; } = [];
}

internal sealed class DemoSellerRow
{
    /// <summary>
    /// Лише префікс пошти: спільний хвіст (@autolot.local) додає сідер, і саме
    /// за ним він потім упізнає власні дані серед справжніх.
    /// </summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>Ім'я людини. Салон має власну назву — вона нижче.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Заповнено — продавець стає власником салону; null — приватна особа.</summary>
    public DemoDealershipRow? Dealership { get; init; }
}

internal sealed class DemoDealershipRow
{
    public string Name { get; init; } = string.Empty;

    /// <summary>Латиницею й унікальний: стає адресою сторінки /dealers/&lt;slug&gt;.</summary>
    public string Slug { get; init; } = string.Empty;

    public string? Description { get; init; }

    public bool IsVerified { get; init; }
}
