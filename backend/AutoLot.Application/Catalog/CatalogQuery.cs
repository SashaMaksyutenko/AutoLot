using AutoLot.Domain.Enums;

namespace AutoLot.Application.Catalog;

/// <summary>
/// Параметри пошуку в каталозі. Усі поля необов'язкові: порожній запит
/// означає «показати все активне».
///
/// Діапазони задаються парами «від / до», і кожну межу можна вказати окремо —
/// «до 5000 доларів» це так само нормальний фільтр, як і «від 2000 до 5000».
///
/// Набори оголошені масивами, а не <c>IReadOnlyList</c>, свідомо: стандартний
/// прив'язувач ASP.NET не вміє створювати інтерфейсні колекції з query-рядка
/// й мовчки лишає їх порожніми — фільтр тоді просто не спрацьовує, без жодної
/// помилки.
/// </summary>
public sealed record CatalogQuery
{
    /// <summary>Пошук за словом у заголовку оголошення.</summary>
    public string? Text { get; init; }

    // ── Модель ───────────────────────────────────────────────────────

    public long? MakeId { get; init; }

    public long? ModelId { get; init; }

    public long? GenerationId { get; init; }

    // ── Ціна ─────────────────────────────────────────────────────────

    /// <summary>
    /// Межі ціни разом із валютою, в якій їх ввели. Порівняння всередині йде
    /// в гривні, тож фільтр «до 5000 USD» знайде і гривневі оголошення.
    /// </summary>
    public decimal? PriceFrom { get; init; }

    public decimal? PriceTo { get; init; }

    public Currency PriceCurrency { get; init; } = Currency.Usd;

    // ── Характеристики ───────────────────────────────────────────────

    public int? YearFrom { get; init; }

    public int? YearTo { get; init; }

    public int? MileageFrom { get; init; }

    public int? MileageTo { get; init; }

    public decimal? EngineVolumeFrom { get; init; }

    public decimal? EngineVolumeTo { get; init; }

    public int? PowerFrom { get; init; }

    public int? PowerTo { get; init; }

    public BodyType[] BodyTypes { get; init; } = [];

    public FuelType[] FuelTypes { get; init; } = [];

    public TransmissionType[] Transmissions { get; init; } = [];

    public DrivetrainType[] Drivetrains { get; init; } = [];

    public CarColor[] Colors { get; init; } = [];

    public CarCondition? Condition { get; init; }

    // ── Стан і походження ────────────────────────────────────────────

    public bool? WasInAccident { get; init; }

    public bool? IsCustomsCleared { get; init; }

    public bool? IsLocatedInUkraine { get; init; }

    public long? ImportedFromCountryId { get; init; }

    // ── Де ───────────────────────────────────────────────────────────

    public long? RegionId { get; init; }

    public long? CityId { get; init; }

    // ── Хто продає ───────────────────────────────────────────────────

    public AccountType? SellerType { get; init; }

    public ListingType? Type { get; init; }

    // ── Інше ─────────────────────────────────────────────────────────

    /// <summary>Опції, які авто має мати **всі** одразу, а не будь-яку з них.</summary>
    public long[] FeatureIds { get; init; } = [];

    /// <summary>Оголошення без жодного фото зазвичай пропускають.</summary>
    public bool? HasPhotos { get; init; }

    public CatalogSort Sort { get; init; } = CatalogSort.Newest;

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;
}
