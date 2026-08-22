using AutoLot.Domain.Enums;

namespace AutoLot.Application.Listings.Dtos;

/// <summary>
/// Характеристики авто в тому вигляді, в якому їх надсилає клієнт при
/// створенні чи редагуванні оголошення. Дзеркалить сутність Car, але без
/// ідентифікаторів, які клієнту знати не належить.
/// </summary>
public sealed record CarSpecification
{
    public string? Vin { get; init; }

    public int Year { get; init; }

    public CarCondition Condition { get; init; } = CarCondition.Used;

    public long MakeId { get; init; }

    public long ModelId { get; init; }

    public long? GenerationId { get; init; }

    public int? Mileage { get; init; }

    public int? OwnerCount { get; init; }

    public FuelType FuelType { get; init; }

    public decimal? EngineVolume { get; init; }

    public int? EnginePower { get; init; }

    public decimal? FuelConsumptionCity { get; init; }

    public decimal? FuelConsumptionHighway { get; init; }

    public decimal? FuelConsumptionCombined { get; init; }

    public decimal? BatteryCapacity { get; init; }

    public int? ElectricRange { get; init; }

    public ChargingPortType? ChargingPort { get; init; }

    public TransmissionType Transmission { get; init; }

    public DrivetrainType Drivetrain { get; init; }

    public BodyType BodyType { get; init; }

    public CarColor Color { get; init; }

    public bool IsMetallic { get; init; }

    public int? SeatCount { get; init; }

    public int? DoorCount { get; init; }

    public EcologyStandard? EcologyStandard { get; init; }

    public long? ManufacturerCountryId { get; init; }

    public long? ImportedFromCountryId { get; init; }

    public bool IsCustomsCleared { get; init; } = true;

    public bool IsLocatedInUkraine { get; init; } = true;

    public bool WasInAccident { get; init; }

    public DamageState DamageState { get; init; } = DamageState.NotDamaged;

    public PaintCondition? PaintCondition { get; init; }

    public bool HasServiceBook { get; init; }

    public bool IsGarageKept { get; init; }

    public bool IsOnCredit { get; init; }

    /// <summary>Ідентифікатори обраних опцій комплектації.</summary>
    public IReadOnlyList<long> FeatureIds { get; init; } = [];
}
