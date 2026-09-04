using AutoLot.Application.Catalog;

namespace AutoLot.Application.Search.Dtos;

/// <summary>Збережений пошук для показу в списку.</summary>
public sealed record SavedSearchCard(
    long Id,
    string Name,

    /// <summary>
    /// Самі фільтри — щоб клієнт міг відновити ними сторінку каталогу.
    /// Віддаємо об'єктом, а не збереженим рядком: рядок довелося б розбирати
    /// ще й на клієнті, а зіпсований — двічі обробляти.
    /// </summary>
    CatalogQuery Query,

    /// <summary>
    /// Скільки авто підходить прямо зараз. Без цього числа список назв
    /// нічого не каже: «Дизельні універсали» — це нуль знахідок чи сорок?
    /// </summary>
    int MatchCount,
    DateTimeOffset CreatedAt);

/// <summary>Тіло запиту на збереження пошуку.</summary>
public sealed record SaveSearchRequest
{
    public string Name { get; init; } = string.Empty;

    /// <summary>Фільтри такі, як їх зібрала сторінка каталогу.</summary>
    public CatalogQuery Query { get; init; } = new();
}

/// <summary>Тіло запиту на перейменування.</summary>
public sealed record RenameSearchRequest
{
    public string Name { get; init; } = string.Empty;
}
