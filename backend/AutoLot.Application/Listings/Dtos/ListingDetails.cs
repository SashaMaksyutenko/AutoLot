using AutoLot.Application.Geo.Dtos;
using AutoLot.Domain.Enums;

namespace AutoLot.Application.Listings.Dtos;

/// <summary>Повна картка оголошення.</summary>
public sealed record ListingDetails(
    long Id,
    string Title,
    string Description,
    ListingType Type,
    ListingStatus Status,
    decimal Price,
    Currency Currency,
    decimal PriceUah,
    bool IsNegotiable,
    bool AcceptsTrade,
    bool IsUrgent,
    UserLocation? Location,
    SellerSummary Seller,
    CarDetails Car,

    /// <summary>
    /// Галерея, головне фото першим. Лежить саме тут, а не за окремим
    /// маршрутом: той доступний лише власникові, а картку авто дивляться всі.
    /// </summary>
    IReadOnlyList<ListingPhoto> Photos,
    DateTimeOffset? PublishedAt,
    DateTimeOffset? ExpiresAt,
    /// <summary>Заповнена лише для автора та модератора.</summary>
    string? RejectionReason,
    int ViewCount,

    /// <summary>Чи відклав це оголошення той, хто зараз дивиться.</summary>
    bool IsFavorite,

    /// <summary>
    /// Салон, якщо продає він. У блоці продавця тоді показуємо салон із
    /// посиланням на вітрину, а не приватну особу: покупцеві важливо, з ким
    /// він має справу.
    /// </summary>
    DealerBadge? Dealer);

public sealed record SellerSummary(
    long Id,
    string DisplayName,
    AccountType AccountType,

    /// <summary>
    /// Телефон продавця — **лише для автентифікованих**. Гість бачить
    /// порожнє поле й кнопку «увійдіть, щоб побачити».
    ///
    /// Це не примха: відкритий номер у публічному JSON збирається роботами
    /// за години, і продавець потім роками отримує дзвінки від посередників.
    /// Реєстрація не зупинить зловмисника, але робить збір номерів помітним
    /// і обмежуваним.
    /// </summary>
    string? PhoneNumber,

    /// <summary>
    /// Скільки відгуків і яка середня оцінка. Показуємо тут, а не окремим
    /// запитом: репутація потрібна саме тоді, коли покупець вирішує, чи
    /// писати — тобто на цій-таки сторінці.
    /// </summary>
    RatingSummary Rating);

/// <summary>
/// Характеристики для показу. На відміну від CarSpecification тут не
/// ідентифікатори, а готові назви — клієнту не треба вантажити довідники,
/// щоб намалювати картку.
/// </summary>
public sealed record CarDetails(
    string? Vin,
    int Year,
    CarCondition Condition,
    string Make,
    string Model,
    string? Generation,
    int? Mileage,
    int? OwnerCount,
    FuelType FuelType,
    decimal? EngineVolume,
    int? EnginePower,
    decimal? FuelConsumptionCity,
    decimal? FuelConsumptionHighway,
    decimal? FuelConsumptionCombined,
    decimal? BatteryCapacity,
    int? ElectricRange,
    ChargingPortType? ChargingPort,
    TransmissionType Transmission,
    DrivetrainType Drivetrain,
    BodyType BodyType,
    CarColor Color,
    bool IsMetallic,
    int? SeatCount,
    int? DoorCount,
    EcologyStandard? EcologyStandard,
    string? ManufacturerCountry,
    string? ImportedFromCountry,
    bool IsCustomsCleared,
    bool IsLocatedInUkraine,
    bool WasInAccident,
    DamageState DamageState,
    PaintCondition? PaintCondition,
    bool HasServiceBook,
    bool IsGarageKept,
    bool IsOnCredit,
    IReadOnlyList<string> Features);
