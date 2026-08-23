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
    DateTimeOffset? PublishedAt,
    DateTimeOffset? ExpiresAt,
    /// <summary>Заповнена лише для автора та модератора.</summary>
    string? RejectionReason,
    int ViewCount);

public sealed record SellerSummary(long Id, string DisplayName, AccountType AccountType);

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
