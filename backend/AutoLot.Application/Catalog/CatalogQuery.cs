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

    /// <summary>Витрата пального в змішаному циклі, л/100 км — «не більше».</summary>
    public decimal? FuelConsumptionTo { get; init; }

    /// <summary>Скільки власників було. «Один власник» — класичний запит.</summary>
    public int? OwnerCountTo { get; init; }

    public int? SeatCountFrom { get; init; }

    public int? DoorCountFrom { get; init; }

    // ── Електромобіль ────────────────────────────────────────────────
    //
    // Ці три поля й є те, за чим обирають електрокар. Фільтр «пальне =
    // Електро» відповідає лише на питання «чи електричний», а покупця
    // цікавить, скільки він проїде й чим заряджається.

    /// <summary>Ємність батареї, кВт·год — «не менше».</summary>
    public decimal? BatteryCapacityFrom { get; init; }

    /// <summary>Запас ходу, км — «не менше».</summary>
    public int? ElectricRangeFrom { get; init; }

    public ChargingPortType[] ChargingPorts { get; init; } = [];

    public BodyType[] BodyTypes { get; init; } = [];

    public FuelType[] FuelTypes { get; init; } = [];

    public TransmissionType[] Transmissions { get; init; } = [];

    public DrivetrainType[] Drivetrains { get; init; } = [];

    public CarColor[] Colors { get; init; } = [];

    public CarCondition? Condition { get; init; }

    public EcologyStandard[] EcologyStandards { get; init; } = [];

    /// <summary>Металік. Порожньо — байдуже.</summary>
    public bool? IsMetallic { get; init; }

    public int? SeatCountTo { get; init; }

    // ── Стан і походження ────────────────────────────────────────────

    public bool? WasInAccident { get; init; }

    public bool? IsCustomsCleared { get; init; }

    public bool? IsLocatedInUkraine { get; init; }

    public long? ImportedFromCountryId { get; init; }

    /// <summary>Країна виробника. Це НЕ те саме, що «звідки пригнали».</summary>
    public long? ManufacturerCountryId { get; init; }

    public DamageState[] DamageStates { get; init; } = [];

    public PaintCondition[] PaintConditions { get; init; } = [];

    /// <summary>Є сервісна книжка.</summary>
    public bool? HasServiceBook { get; init; }

    /// <summary>Зберігалося в гаражі.</summary>
    public bool? IsGarageKept { get; init; }

    /// <summary>
    /// Чи авто в кредиті. Покупці найчастіше шукають ті, що НЕ в кредиті,
    /// тож фільтр тризначний, а не «показати кредитні».
    /// </summary>
    public bool? IsOnCredit { get; init; }

    // ── Де ───────────────────────────────────────────────────────────

    public long? RegionId { get; init; }

    public long? CityId { get; init; }

    /// <summary>Район міста. У великих містах відстань вирішує все.</summary>
    public long? CityDistrictId { get; init; }

    // ── Хто продає ───────────────────────────────────────────────────


    public ListingType? Type { get; init; }

    // ── Інше ─────────────────────────────────────────────────────────

    /// <summary>Опції, які авто має мати **всі** одразу, а не будь-яку з них.</summary>
    public long[] FeatureIds { get; init; } = [];

    /// <summary>Торг доречний.</summary>
    public bool? IsNegotiable { get; init; }

    /// <summary>Продавець розглядає обмін.</summary>
    public bool? AcceptsTrade { get; init; }

    /// <summary>Терміновий продаж.</summary>
    public bool? IsUrgent { get; init; }

    /// <summary>Оголошення без жодного фото зазвичай пропускають.</summary>
    public bool? HasPhotos { get; init; }

    // ── Продавець ────────────────────────────────────────────────────

    /// <summary>Вітрина конкретного салону: всі його оголошення й тільки вони.</summary>
    public long? DealershipId { get; init; }

    /// <summary>
    /// Хто продає. <c>true</c> — лише салони, <c>false</c> — лише приватні
    /// особи, порожньо — усі. Саме тризначний вибір, а не прапорець: обидва
    /// боки цього фільтра однаково потрібні. Одні шукають гарантію салону,
    /// інші свідомо йдуть до приватника, щоб не переплачувати.
    ///
    /// Питається саме про НАЛЕЖНІСТЬ ЛОТА салону, а не про тип акаунта
    /// продавця. Донедавна поруч жив другий фільтр, який питав друге, і
    /// відповіді могли розійтися: працівник салону з дилерським акаунтом
    /// може подати й особисте оголошення. Після появи сутності салону
    /// правильна відповідь одна, тож і фільтр лишився один.
    /// </summary>
    public bool? FromDealer { get; init; }

    /// <summary>Лише салони з бейджем перевіреного.</summary>
    public bool? VerifiedDealerOnly { get; init; }

    public CatalogSort Sort { get; init; } = CatalogSort.Newest;

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;
}
