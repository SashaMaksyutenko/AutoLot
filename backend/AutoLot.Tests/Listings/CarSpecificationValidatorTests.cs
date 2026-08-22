using AutoLot.Application.Listings.Dtos;
using AutoLot.Application.Listings.Validation;
using AutoLot.Domain.Enums;
using AutoLot.Tests.TestDoubles;

namespace AutoLot.Tests.Listings;

/// <summary>
/// Найважливіші тести цього шару. Бензинові й електричні поля лежать в одній
/// таблиці, тож база не завадить зберегти електромобіль з об'ємом двигуна —
/// це має зробити валідатор, і саме тут перевіряється, що він робить.
/// </summary>
public class CarSpecificationValidatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);

    private readonly CarSpecificationValidator validator = new(new FixedClock(Now));

    [Fact]
    public void Accepts_a_plain_petrol_car()
    {
        Assert.True(validator.Validate(Petrol()).IsValid);
    }

    [Fact]
    public void Accepts_an_electric_car()
    {
        Assert.True(validator.Validate(Electric()).IsValid);
    }

    // ── Пальне та двигун ─────────────────────────────────────────────

    [Fact]
    public void Rejects_an_electric_car_with_engine_volume()
    {
        var result = validator.Validate(Electric() with { EngineVolume = 1.6m });

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CarSpecification.EngineVolume));
    }

    [Fact]
    public void Rejects_an_electric_car_without_battery()
    {
        var result = validator.Validate(Electric() with { BatteryCapacity = null });

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CarSpecification.BatteryCapacity));
    }

    [Fact]
    public void Rejects_an_electric_car_with_fuel_consumption()
    {
        var result = validator.Validate(Electric() with { FuelConsumptionCombined = 7.5m });

        Assert.Contains(
            result.Errors,
            error => error.PropertyName == nameof(CarSpecification.FuelConsumptionCombined));
    }

    [Fact]
    public void Rejects_a_petrol_car_without_engine_volume()
    {
        var result = validator.Validate(Petrol() with { EngineVolume = null });

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CarSpecification.EngineVolume));
    }

    [Fact]
    public void Rejects_a_petrol_car_with_a_battery()
    {
        var result = validator.Validate(Petrol() with { BatteryCapacity = 60m });

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CarSpecification.BatteryCapacity));
    }

    [Fact]
    public void Rejects_a_petrol_car_with_a_charging_port()
    {
        var result = validator.Validate(Petrol() with { ChargingPort = ChargingPortType.Type2 });

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CarSpecification.ChargingPort));
    }

    [Theory]
    [InlineData(FuelType.Hybrid)]
    [InlineData(FuelType.PluginHybrid)]
    public void Allows_a_hybrid_to_have_both_engine_and_battery(FuelType fuelType)
    {
        var hybrid = Petrol() with
        {
            FuelType = fuelType,
            EngineVolume = 1.8m,
            BatteryCapacity = 8.8m,
            ElectricRange = 50,
            ChargingPort = ChargingPortType.Type2,
        };

        Assert.True(validator.Validate(hybrid).IsValid);
    }

    [Fact]
    public void Hydrogen_car_has_no_engine_volume_and_no_battery()
    {
        var hydrogen = Electric() with
        {
            FuelType = FuelType.Hydrogen,
            BatteryCapacity = null,
            ElectricRange = null,
            ChargingPort = null,
        };

        Assert.True(validator.Validate(hydrogen).IsValid);
    }

    // ── Новий чи вживаний ────────────────────────────────────────────

    [Fact]
    public void Rejects_a_used_car_without_mileage()
    {
        var result = validator.Validate(Petrol() with { Mileage = null });

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CarSpecification.Mileage));
    }

    [Fact]
    public void Rejects_a_new_car_with_previous_owners()
    {
        var result = validator.Validate(New() with { OwnerCount = 2 });

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CarSpecification.OwnerCount));
    }

    [Fact]
    public void Rejects_a_new_car_with_serious_mileage()
    {
        var result = validator.Validate(New() with { Mileage = 60_000 });

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CarSpecification.Mileage));
    }

    [Fact]
    public void Accepts_a_new_car_driven_from_the_dealership()
    {
        Assert.True(validator.Validate(New() with { Mileage = 12 }).IsValid);
    }

    // ── Рік і VIN ────────────────────────────────────────────────────

    [Fact]
    public void Accepts_next_year_because_new_models_are_sold_in_advance()
    {
        Assert.True(validator.Validate(Petrol() with { Year = Now.Year + 1 }).IsValid);
    }

    [Theory]
    [InlineData(1949)]
    [InlineData(2050)]
    public void Rejects_an_impossible_year(int year)
    {
        var result = validator.Validate(Petrol() with { Year = year });

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CarSpecification.Year));
    }

    [Fact]
    public void Accepts_a_well_formed_vin()
    {
        Assert.True(validator.Validate(Petrol() with { Vin = "WVWZZZ1JZXW000001" }).IsValid);
    }

    [Theory]
    [InlineData("TOOSHORT")]
    [InlineData("WVWZZZ1JZXW00000I")]
    [InlineData("WVWZZZ1JZXW00000O")]
    [InlineData("WVWZZZ1JZXW00000Q")]
    public void Rejects_a_malformed_vin(string vin)
    {
        var result = validator.Validate(Petrol() with { Vin = vin });

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CarSpecification.Vin));
    }

    [Fact]
    public void Accepts_an_absent_vin()
    {
        Assert.True(validator.Validate(Petrol() with { Vin = null }).IsValid);
    }

    // ── Кузов і опції ────────────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(12)]
    public void Rejects_an_impossible_seat_count(int seats)
    {
        var result = validator.Validate(Petrol() with { SeatCount = seats });

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CarSpecification.SeatCount));
    }

    [Fact]
    public void Rejects_the_same_feature_twice()
    {
        var result = validator.Validate(Petrol() with { FeatureIds = [3, 7, 3] });

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CarSpecification.FeatureIds));
    }

    private static CarSpecification Petrol() => new()
    {
        Year = 2015,
        Condition = CarCondition.Used,
        MakeId = 1,
        ModelId = 2,
        Mileage = 120_000,
        OwnerCount = 2,
        FuelType = FuelType.Petrol,
        EngineVolume = 1.6m,
        EnginePower = 110,
        FuelConsumptionCombined = 7.2m,
        Transmission = TransmissionType.Manual,
        Drivetrain = DrivetrainType.FrontWheel,
        BodyType = BodyType.Sedan,
        Color = CarColor.Black,
        SeatCount = 5,
        DoorCount = 4,
    };

    private static CarSpecification Electric() => Petrol() with
    {
        FuelType = FuelType.Electric,
        EngineVolume = null,
        FuelConsumptionCombined = null,
        BatteryCapacity = 77.4m,
        ElectricRange = 480,
        ChargingPort = ChargingPortType.Ccs,
    };

    private static CarSpecification New() => Petrol() with
    {
        Condition = CarCondition.New,
        Year = Now.Year,
        Mileage = 0,
        OwnerCount = null,
    };
}
