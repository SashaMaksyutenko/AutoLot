using AutoLot.Domain.Cars;
using AutoLot.Domain.Common;
using AutoLot.Domain.Enums;
using AutoLot.Domain.Geo;

namespace AutoLot.Domain.Listings;

/// <summary>
/// Технічні характеристики авто з оголошення. Живе рівно одне на оголошення,
/// тому FK тут, а не навпаки.
///
/// Поля бензинового й електричного авто свідомо лежать в одній таблиці, а не
/// в різних (SPEC §3): розділення дало б дві майже однакові сутності й
/// подвоїло б кожен запит до каталогу. Натомість узгодженість забезпечує
/// валідація — електромобіль з об'ємом двигуна просто не збережеться.
/// </summary>
public sealed class Car : Entity
{
    public long ListingId { get; set; }

    public Listing Listing { get; set; } = null!;

    // ── Ідентифікація ────────────────────────────────────────────────

    /// <summary>Необов'язковий: продавці часто не вказують VIN.</summary>
    public string? Vin { get; set; }

    public int Year { get; set; }

    public CarCondition Condition { get; set; } = CarCondition.Used;

    // ── Модель ───────────────────────────────────────────────────────

    public long MakeId { get; set; }

    public Make Make { get; set; } = null!;

    public long ModelId { get; set; }

    public Model Model { get; set; } = null!;

    /// <summary>Покоління заповнене не для всіх моделей довідника.</summary>
    public long? GenerationId { get; set; }

    public Generation? Generation { get; set; }

    // ── Пробіг і власники ────────────────────────────────────────────

    /// <summary>Кілометри. У нового авто порожній.</summary>
    public int? Mileage { get; set; }

    /// <summary>Скільки власників було в Україні. У нового порожній.</summary>
    public int? OwnerCount { get; set; }

    // ── Двигун ───────────────────────────────────────────────────────

    public FuelType FuelType { get; set; }

    /// <summary>Літри. Порожній в електромобіля.</summary>
    public decimal? EngineVolume { get; set; }

    /// <summary>Кінські сили.</summary>
    public int? EnginePower { get; set; }

    public decimal? FuelConsumptionCity { get; set; }

    public decimal? FuelConsumptionHighway { get; set; }

    public decimal? FuelConsumptionCombined { get; set; }

    // ── Електрика ────────────────────────────────────────────────────

    /// <summary>Ємність батареї, кВт·год. Обов'язкова для електромобіля.</summary>
    public decimal? BatteryCapacity { get; set; }

    /// <summary>Запас ходу на одному заряді, км.</summary>
    public int? ElectricRange { get; set; }

    public ChargingPortType? ChargingPort { get; set; }

    // ── Трансмісія й кузов ───────────────────────────────────────────

    public TransmissionType Transmission { get; set; }

    public DrivetrainType Drivetrain { get; set; }

    public BodyType BodyType { get; set; }

    public CarColor Color { get; set; }

    public bool IsMetallic { get; set; }

    public int? SeatCount { get; set; }

    public int? DoorCount { get; set; }

    public EcologyStandard? EcologyStandard { get; set; }

    // ── Походження ───────────────────────────────────────────────────

    public long? ManufacturerCountryId { get; set; }

    public Country? ManufacturerCountry { get; set; }

    /// <summary>Звідки пригнали. Порожня в авто, купленого в Україні.</summary>
    public long? ImportedFromCountryId { get; set; }

    public Country? ImportedFromCountry { get; set; }

    public bool IsCustomsCleared { get; set; } = true;

    /// <summary>Хибне для авто «під замовлення», яке ще за кордоном.</summary>
    public bool IsLocatedInUkraine { get; set; } = true;

    // ── Історія та стан ──────────────────────────────────────────────

    public bool WasInAccident { get; set; }

    public DamageState DamageState { get; set; } = DamageState.NotDamaged;

    public PaintCondition? PaintCondition { get; set; }

    public bool HasServiceBook { get; set; }

    public bool IsGarageKept { get; set; }

    /// <summary>Авто в заставі чи кредиті — покупцю це треба знати наперед.</summary>
    public bool IsOnCredit { get; set; }

    // ── Комплектація та фото ─────────────────────────────────────────

    public ICollection<CarFeature> Features { get; } = [];

    public ICollection<CarPhoto> Photos { get; } = [];
}
