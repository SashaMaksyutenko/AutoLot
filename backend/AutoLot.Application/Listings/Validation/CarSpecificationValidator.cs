using AutoLot.Application.Common.Abstractions;
using AutoLot.Application.Listings.Dtos;
using AutoLot.Domain.Enums;
using FluentValidation;

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
            .WithMessage("VIN складається з 17 символів; літери I, O та Q в ньому не використовуються.")
            .When(car => !string.IsNullOrWhiteSpace(car.Vin));

        RuleFor(car => car.MakeId).GreaterThan(0).WithMessage("Оберіть марку.");
        RuleFor(car => car.ModelId).GreaterThan(0).WithMessage("Оберіть модель.");

        RuleFor(car => car.GenerationId)
            .GreaterThan(0).WithMessage("Некоректне покоління.")
            .When(car => car.GenerationId.HasValue);

        ApplyConditionRules();
        ApplyEngineRules();
        ApplyElectricRules();
        ApplyBodyRules();

        RuleFor(car => car.FeatureIds)
            .Must(features => features.Distinct().Count() == features.Count)
            .WithMessage("Одна опція вказана двічі.");

        RuleForEach(car => car.FeatureIds)
            .GreaterThan(0).WithMessage("Некоректна опція комплектації.");
    }

    /// <summary>Новий чи вживаний — від цього залежить, які поля взагалі мають сенс.</summary>
    private void ApplyConditionRules()
    {
        RuleFor(car => car.Condition).IsInEnum().WithMessage("Невідомий стан авто.");

        RuleFor(car => car.Mileage)
            .NotNull().WithMessage("Вкажіть пробіг.")
            .When(car => car.Condition == CarCondition.Used);

        RuleFor(car => car.Mileage)
            .InclusiveBetween(0, 3_000_000).WithMessage("Пробіг виглядає нереальним.")
            .When(car => car.Mileage.HasValue);

        // У нового авто пробіг буває нульовим після перегону, але не тисячним.
        RuleFor(car => car.Mileage)
            .LessThanOrEqualTo(1000)
            .WithMessage("Для нового авто пробіг не може бути таким великим.")
            .When(car => car.Condition == CarCondition.New && car.Mileage.HasValue);

        RuleFor(car => car.OwnerCount)
            .Empty().WithMessage("У нового авто ще не було власників.")
            .When(car => car.Condition == CarCondition.New);

        RuleFor(car => car.OwnerCount)
            .InclusiveBetween(1, 50).WithMessage("Кількість власників виглядає нереальною.")
            .When(car => car.OwnerCount.HasValue);
    }

    private void ApplyEngineRules()
    {
        RuleFor(car => car.FuelType).IsInEnum().WithMessage("Невідомий тип пального.");
        RuleFor(car => car.Transmission).IsInEnum().WithMessage("Невідомий тип коробки передач.");
        RuleFor(car => car.Drivetrain).IsInEnum().WithMessage("Невідомий тип приводу.");

        // Об'єм двигуна обов'язковий скрізь, крім електро й водню: там двигуна
        // внутрішнього згоряння просто немає.
        RuleFor(car => car.EngineVolume)
            .NotNull().WithMessage("Вкажіть об'єм двигуна.")
            .When(car => HasCombustionEngine(car.FuelType));

        RuleFor(car => car.EngineVolume)
            .Empty().WithMessage("В електромобіля немає об'єму двигуна.")
            .When(car => !HasCombustionEngine(car.FuelType));

        RuleFor(car => car.EngineVolume)
            .InclusiveBetween(0.1m, 12.0m).WithMessage("Об'єм двигуна має бути від 0,1 до 12 літрів.")
            .When(car => car.EngineVolume.HasValue);

        RuleFor(car => car.EnginePower)
            .InclusiveBetween(1, 2000).WithMessage("Потужність має бути від 1 до 2000 к.с.")
            .When(car => car.EnginePower.HasValue);

        RuleFor(car => car.FuelConsumptionCity)
            .InclusiveBetween(0.1m, 50m).WithMessage("Витрата в місті виглядає нереальною.")
            .When(car => car.FuelConsumptionCity.HasValue);

        RuleFor(car => car.FuelConsumptionHighway)
            .InclusiveBetween(0.1m, 50m).WithMessage("Витрата на шосе виглядає нереальною.")
            .When(car => car.FuelConsumptionHighway.HasValue);

        RuleFor(car => car.FuelConsumptionCombined)
            .InclusiveBetween(0.1m, 50m).WithMessage("Змішана витрата виглядає нереальною.")
            .When(car => car.FuelConsumptionCombined.HasValue);

        RuleFor(car => car.FuelConsumptionCombined)
            .Empty().WithMessage("Електромобіль не витрачає пального.")
            .When(car => car.FuelType is FuelType.Electric);
    }

    private void ApplyElectricRules()
    {
        // Батарея обов'язкова там, де без неї машина не поїде взагалі.
        RuleFor(car => car.BatteryCapacity)
            .NotNull().WithMessage("Вкажіть ємність батареї.")
            .When(car => car.FuelType is FuelType.Electric);

        RuleFor(car => car.BatteryCapacity)
            .Empty().WithMessage("Батарея буває лише в електромобіля або гібрида.")
            .When(car => !HasBattery(car.FuelType));

        RuleFor(car => car.BatteryCapacity)
            .InclusiveBetween(1m, 300m).WithMessage("Ємність батареї має бути від 1 до 300 кВт·год.")
            .When(car => car.BatteryCapacity.HasValue);

        RuleFor(car => car.ElectricRange)
            .InclusiveBetween(1, 1500).WithMessage("Запас ходу виглядає нереальним.")
            .When(car => car.ElectricRange.HasValue);

        RuleFor(car => car.ElectricRange)
            .Empty().WithMessage("Запас ходу вказують лише там, де є батарея.")
            .When(car => !HasBattery(car.FuelType));

        RuleFor(car => car.ChargingPort)
            .Empty().WithMessage("Зарядний роз'єм буває лише там, де є батарея.")
            .When(car => !HasBattery(car.FuelType));

        RuleFor(car => car.ChargingPort)
            .IsInEnum().WithMessage("Невідомий тип зарядного роз'єму.")
            .When(car => car.ChargingPort.HasValue);
    }

    private void ApplyBodyRules()
    {
        RuleFor(car => car.BodyType).IsInEnum().WithMessage("Невідомий тип кузова.");
        RuleFor(car => car.Color).IsInEnum().WithMessage("Невідомий колір.");
        RuleFor(car => car.DamageState).IsInEnum().WithMessage("Невідомий стан пошкодження.");

        RuleFor(car => car.SeatCount)
            .InclusiveBetween(1, 9).WithMessage("Кількість місць має бути від 1 до 9.")
            .When(car => car.SeatCount.HasValue);

        RuleFor(car => car.DoorCount)
            .InclusiveBetween(2, 6).WithMessage("Кількість дверей має бути від 2 до 6.")
            .When(car => car.DoorCount.HasValue);

        RuleFor(car => car.EcologyStandard)
            .IsInEnum().WithMessage("Невідомий екологічний стандарт.")
            .When(car => car.EcologyStandard.HasValue);

        RuleFor(car => car.PaintCondition)
            .IsInEnum().WithMessage("Невідомий стан лакофарбового покриття.")
            .When(car => car.PaintCondition.HasValue);

        RuleFor(car => car.ImportedFromCountryId)
            .GreaterThan(0).WithMessage("Некоректна країна пригону.")
            .When(car => car.ImportedFromCountryId.HasValue);

        RuleFor(car => car.ManufacturerCountryId)
            .GreaterThan(0).WithMessage("Некоректна країна-виробник.")
            .When(car => car.ManufacturerCountryId.HasValue);
    }

    /// <summary>Чи має авто двигун внутрішнього згоряння — і, отже, об'єм.</summary>
    private static bool HasCombustionEngine(FuelType fuelType) =>
        fuelType is not (FuelType.Electric or FuelType.Hydrogen);

    /// <summary>Чи має авто тягову батарею — і, отже, запас ходу й роз'єм.</summary>
    private static bool HasBattery(FuelType fuelType) =>
        fuelType is FuelType.Electric or FuelType.Hybrid or FuelType.PluginHybrid;
}
