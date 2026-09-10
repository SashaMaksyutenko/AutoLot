using AutoLot.Application.Common.Abstractions;
using AutoLot.Application.Listings.Dtos;
using AutoLot.Domain.Enums;
using FluentValidation;
using AutoLot.Application.Common.Localization;

namespace AutoLot.Application.Listings.Validation;

/// <summary>
/// Стежить, щоб характеристики авто були узгоджені між собою.
///
/// Це та ціна, яку ми платимо за рішення тримати бензинові й електричні поля
/// в одній таблиці (SPEC §3): база сама по собі дозволить електромобіль
/// з об'ємом двигуна 1.6, тож не дозволити має код. Натомість ми маємо одну
/// сутність замість двох і один запит замість двох.
/// </summary>
public sealed class CarSpecificationValidator : AbstractValidator<CarSpecification>
{
    /// <summary>Перший серійний автомобіль з'явився значно раніше, але оголошення про них не подають.</summary>
    private const int EarliestYear = 1950;

    /// <summary>У VIN не буває I, O та Q — їх плутають з одиницею та нулем.</summary>
    private const string VinPattern = "^[A-HJ-NPR-Z0-9]{17}$";

    public CarSpecificationValidator(IDateTimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);

        // Наступний рік допустимий: нові моделі продають наперед.
        var latestYear = clock.UtcNow.Year + 1;

        RuleFor(car => car.Year)
            .InclusiveBetween(EarliestYear, latestYear)
            .WithMessage($"Рік випуску має бути між {EarliestYear} та {latestYear}.");

        RuleFor(car => car.Vin)
            .Matches(VinPattern)
            .WithMessage(MessageCodes.CarVinFormat)
            .When(car => !string.IsNullOrWhiteSpace(car.Vin));

        RuleFor(car => car.MakeId).GreaterThan(0).WithMessage(MessageCodes.CarMakeRequired);
        RuleFor(car => car.ModelId).GreaterThan(0).WithMessage(MessageCodes.CarModelRequired);

        RuleFor(car => car.GenerationId)
            .GreaterThan(0).WithMessage(MessageCodes.CarGenerationInvalid)
            .When(car => car.GenerationId.HasValue);

        ApplyConditionRules();
        ApplyEngineRules();
        ApplyElectricRules();
        ApplyBodyRules();

        RuleFor(car => car.FeatureIds)
            .Must(features => features.Distinct().Count() == features.Count)
            .WithMessage(MessageCodes.CarFeaturesDuplicate);

        RuleForEach(car => car.FeatureIds)
            .GreaterThan(0).WithMessage(MessageCodes.CarFeaturesInvalid);
    }

    /// <summary>Новий чи вживаний — від цього залежить, які поля взагалі мають сенс.</summary>
    private void ApplyConditionRules()
    {
        RuleFor(car => car.Condition).IsInEnum().WithMessage(MessageCodes.CarConditionUnknown);

        RuleFor(car => car.Mileage)
            .NotNull().WithMessage(MessageCodes.CarMileageRequired)
            .When(car => car.Condition == CarCondition.Used);

        RuleFor(car => car.Mileage)
            .InclusiveBetween(0, 3_000_000).WithMessage(MessageCodes.CarMileageUnrealistic)
            .When(car => car.Mileage.HasValue);

        // У нового авто пробіг буває нульовим після перегону, але не тисячним.
        RuleFor(car => car.Mileage)
            .LessThanOrEqualTo(1000)
            .WithMessage(MessageCodes.CarMileageNewTooHigh)
            .When(car => car.Condition == CarCondition.New && car.Mileage.HasValue);

        RuleFor(car => car.OwnerCount)
            .Empty().WithMessage(MessageCodes.CarOwnersNewHasNone)
            .When(car => car.Condition == CarCondition.New);

        RuleFor(car => car.OwnerCount)
            .InclusiveBetween(1, 50).WithMessage(MessageCodes.CarOwnersUnrealistic)
            .When(car => car.OwnerCount.HasValue);
    }

    private void ApplyEngineRules()
    {
        RuleFor(car => car.FuelType).IsInEnum().WithMessage(MessageCodes.CarFuelUnknown);
        RuleFor(car => car.Transmission).IsInEnum().WithMessage(MessageCodes.CarTransmissionUnknown);
        RuleFor(car => car.Drivetrain).IsInEnum().WithMessage(MessageCodes.CarDrivetrainUnknown);

        // Об'єм двигуна обов'язковий скрізь, крім електро й водню: там двигуна
        // внутрішнього згоряння просто немає.
        RuleFor(car => car.EngineVolume)
            .NotNull().WithMessage(MessageCodes.CarEngineRequired)
            .When(car => HasCombustionEngine(car.FuelType));

        RuleFor(car => car.EngineVolume)
            .Empty().WithMessage(MessageCodes.CarEngineNotForElectric)
            .When(car => !HasCombustionEngine(car.FuelType));

        RuleFor(car => car.EngineVolume)
            .InclusiveBetween(0.1m, 12.0m).WithMessage(MessageCodes.CarEngineRange)
            .When(car => car.EngineVolume.HasValue);

        RuleFor(car => car.EnginePower)
            .InclusiveBetween(1, 2000).WithMessage(MessageCodes.CarPowerRange)
            .When(car => car.EnginePower.HasValue);

        RuleFor(car => car.FuelConsumptionCity)
            .InclusiveBetween(0.1m, 50m).WithMessage(MessageCodes.CarConsumptionCityUnrealistic)
            .When(car => car.FuelConsumptionCity.HasValue);

        RuleFor(car => car.FuelConsumptionHighway)
            .InclusiveBetween(0.1m, 50m).WithMessage(MessageCodes.CarConsumptionHighwayUnrealistic)
            .When(car => car.FuelConsumptionHighway.HasValue);

        RuleFor(car => car.FuelConsumptionCombined)
            .InclusiveBetween(0.1m, 50m).WithMessage(MessageCodes.CarConsumptionCombinedUnrealistic)
            .When(car => car.FuelConsumptionCombined.HasValue);

        RuleFor(car => car.FuelConsumptionCombined)
            .Empty().WithMessage(MessageCodes.CarConsumptionNotForElectric)
            .When(car => car.FuelType is FuelType.Electric);
    }

    private void ApplyElectricRules()
    {
        // Батарея обов'язкова там, де без неї машина не поїде взагалі.
        RuleFor(car => car.BatteryCapacity)
            .NotNull().WithMessage(MessageCodes.CarBatteryRequired)
            .When(car => car.FuelType is FuelType.Electric);

        RuleFor(car => car.BatteryCapacity)
            .Empty().WithMessage(MessageCodes.CarBatteryOnlyElectric)
            .When(car => !HasBattery(car.FuelType));

        RuleFor(car => car.BatteryCapacity)
            .InclusiveBetween(1m, 300m).WithMessage(MessageCodes.CarBatteryRange)
            .When(car => car.BatteryCapacity.HasValue);

        RuleFor(car => car.ElectricRange)
            .InclusiveBetween(1, 1500).WithMessage(MessageCodes.CarRangeUnrealistic)
            .When(car => car.ElectricRange.HasValue);

        RuleFor(car => car.ElectricRange)
            .Empty().WithMessage(MessageCodes.CarRangeNeedsBattery)
            .When(car => !HasBattery(car.FuelType));

        RuleFor(car => car.ChargingPort)
            .Empty().WithMessage(MessageCodes.CarChargingPortNeedsBattery)
            .When(car => !HasBattery(car.FuelType));

        RuleFor(car => car.ChargingPort)
            .IsInEnum().WithMessage(MessageCodes.CarChargingPortUnknown)
            .When(car => car.ChargingPort.HasValue);
    }

    private void ApplyBodyRules()
    {
        RuleFor(car => car.BodyType).IsInEnum().WithMessage(MessageCodes.CarBodyUnknown);
        RuleFor(car => car.Color).IsInEnum().WithMessage(MessageCodes.CarColourUnknown);
        RuleFor(car => car.DamageState).IsInEnum().WithMessage(MessageCodes.CarDamageUnknown);

        RuleFor(car => car.SeatCount)
            .InclusiveBetween(1, 9).WithMessage(MessageCodes.CarSeatsRange)
            .When(car => car.SeatCount.HasValue);

        RuleFor(car => car.DoorCount)
            .InclusiveBetween(2, 6).WithMessage(MessageCodes.CarDoorsRange)
            .When(car => car.DoorCount.HasValue);

        RuleFor(car => car.EcologyStandard)
            .IsInEnum().WithMessage(MessageCodes.CarEcologyUnknown)
            .When(car => car.EcologyStandard.HasValue);

        RuleFor(car => car.PaintCondition)
            .IsInEnum().WithMessage(MessageCodes.CarPaintUnknown)
            .When(car => car.PaintCondition.HasValue);

        RuleFor(car => car.ImportedFromCountryId)
            .GreaterThan(0).WithMessage(MessageCodes.CarImportedFromInvalid)
            .When(car => car.ImportedFromCountryId.HasValue);

        RuleFor(car => car.ManufacturerCountryId)
            .GreaterThan(0).WithMessage(MessageCodes.CarManufacturerCountryInvalid)
            .When(car => car.ManufacturerCountryId.HasValue);
    }

    /// <summary>Чи має авто двигун внутрішнього згоряння — і, отже, об'єм.</summary>
    private static bool HasCombustionEngine(FuelType fuelType) =>
        fuelType is not (FuelType.Electric or FuelType.Hydrogen);

    /// <summary>Чи має авто тягову батарею — і, отже, запас ходу й роз'єм.</summary>
    private static bool HasBattery(FuelType fuelType) =>
        fuelType is FuelType.Electric or FuelType.Hybrid or FuelType.PluginHybrid;
}
