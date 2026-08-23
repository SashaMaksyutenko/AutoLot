using AutoLot.Application.Listings.Dtos;
using AutoLot.Application.Listings.Validation;
using AutoLot.Domain.Enums;
using AutoLot.Tests.TestDoubles;

namespace AutoLot.Tests.Listings;

public class CreateListingRequestValidatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);

    private readonly CreateListingRequestValidator validator =
        new(new CarSpecificationValidator(new FixedClock(Now)));

    [Fact]
    public void Accepts_a_complete_request()
    {
        Assert.True(validator.Validate(Request()).IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Коротко")]
    public void Rejects_a_missing_or_short_title(string title)
    {
        var result = validator.Validate(Request() with { Title = title });

        Assert.Contains(result.Errors, error => error.PropertyName == "Title");
    }

    [Fact]
    public void Rejects_a_short_description()
    {
        var result = validator.Validate(Request() with { Description = "Продам" });

        Assert.Contains(result.Errors, error => error.PropertyName == "Description");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1000)]
    public void Rejects_a_non_positive_price(decimal price)
    {
        var result = validator.Validate(Request() with { Price = price });

        Assert.Contains(result.Errors, error => error.PropertyName == "Price");
    }

    [Fact]
    public void Rejects_a_price_that_looks_like_a_typo()
    {
        var result = validator.Validate(Request() with { Price = 500_000_000m });

        Assert.Contains(result.Errors, error => error.PropertyName == "Price");
    }

    [Fact]
    public void Rejects_a_missing_city()
    {
        var result = validator.Validate(Request() with { CityId = 0 });

        Assert.Contains(result.Errors, error => error.PropertyName == "CityId");
    }

    [Fact]
    public void Car_specification_is_validated_together_with_the_listing()
    {
        // Без цього правила електромобіль з об'ємом двигуна проліз би
        // повз перевірку, бо валідатор оголошення сам по собі його не бачить.
        var request = Request() with
        {
            Car = Request().Car with
            {
                FuelType = FuelType.Electric,
                EngineVolume = 1.6m,
                BatteryCapacity = 60m,
            },
        };

        var result = validator.Validate(request);

        Assert.Contains(result.Errors, error => error.PropertyName.Contains("EngineVolume", StringComparison.Ordinal));
    }

    private static CreateListingRequest Request() => new()
    {
        Title = "Volkswagen Passat B7 2013 у гарному стані",
        Description = "Одна власниця, обслуговування за регламентом, без ДТП.",
        CityId = 1,
        Price = 9500,
        Currency = Currency.Usd,
        Type = ListingType.FixedPrice,
        Car = new CarSpecification
        {
            Year = 2013,
            Condition = CarCondition.Used,
            MakeId = 1,
            ModelId = 2,
            Mileage = 210_000,
            OwnerCount = 1,
            FuelType = FuelType.Diesel,
            EngineVolume = 2.0m,
            EnginePower = 140,
            Transmission = TransmissionType.Manual,
            Drivetrain = DrivetrainType.FrontWheel,
            BodyType = BodyType.Universal,
            Color = CarColor.Grey,
        },
    };
}
