using AutoLot.Application.Common.Abstractions;
using AutoLot.Domain.Cars;
using AutoLot.Domain.Enums;
using AutoLot.Domain.Listings;

namespace AutoLot.Infrastructure.Listings;

/// <summary>
/// Складання демонстраційного авто: характеристики, комплектація, фото.
///
/// Винесено з <see cref="DemoDataSeeder"/>, бо це окрема робота: сідер
/// вирішує, СКІЛЬКИ оголошень і від кого, а тут — яким виходить одне авто.
/// Разом ці дві задачі давали клас на сімсот рядків, у якому правила про
/// батарею електромобіля губилися між запитами до бази.
/// </summary>
internal static class DemoCars
{
    /// <summary>
    /// Збирає технічні характеристики демонстраційного авто.
    ///
    /// Головна вимога до цих даних — несуперечливість. Каталог дозволяє
    /// фільтрувати за двома десятками ознак, і якщо серед них трапиться
    /// електромобіль із витратою пального, фільтр покаже його там, де його
    /// бути не може. Тому набори полів тут тримаються тих самих правил, які
    /// перевіряє CarSpecificationValidator у справжній формі.
    /// </summary>
    public static Car Build(
        Random random,
        long makeId,
        long modelId,
        int year,
        bool isNew,
        FuelType fuelType,
        List<long> featureIds,
        List<long> countryIds)
    {
        var isElectric = fuelType is FuelType.Electric;
        var hasBattery = fuelType is FuelType.Electric or FuelType.Hybrid or FuelType.PluginHybrid;

        var car = new Car
        {
            Year = year,
            Condition = isNew ? CarCondition.New : CarCondition.Used,
            MakeId = makeId,
            ModelId = modelId,
            Mileage = isNew ? random.Next(0, 100) : random.Next(5, 400) * 1000,
            OwnerCount = isNew ? null : random.Next(1, 4),
            FuelType = fuelType,

            // Набори полів мають лишатися узгодженими між собою — тими самими
            // правилами, які перевіряє CarSpecificationValidator.
            EngineVolume = isElectric ? null : Math.Round(random.Next(10, 45) / 10m, 1),
            EnginePower = random.Next(75, 400),
            FuelConsumptionCombined = isElectric ? null : Math.Round(random.Next(45, 130) / 10m, 1),
            BatteryCapacity = hasBattery ? random.Next(8, 100) : null,
            ElectricRange = hasBattery ? random.Next(40, 600) : null,
            ChargingPort = hasBattery ? ChargingPortType.Type2 : null,
            Transmission = (TransmissionType)random.Next(0, 4),
            Drivetrain = (DrivetrainType)random.Next(0, 3),
            BodyType = (BodyType)random.Next(0, 11),
            Color = (CarColor)random.Next(0, 14),
            IsMetallic = random.Next(2) == 0,
            SeatCount = 5,
            DoorCount = random.Next(2) == 0 ? 4 : 5,
            EcologyStandard = (EcologyStandard)random.Next(3, 7),
            IsCustomsCleared = random.Next(10) > 0,
            IsLocatedInUkraine = random.Next(20) > 0,
            WasInAccident = random.Next(5) == 0,
            HasServiceBook = random.Next(3) > 0,
            IsGarageKept = random.Next(3) > 0,
        };

        if (countryIds.Count > 0 && random.Next(2) == 0)
        {
            car.ImportedFromCountryId = countryIds[random.Next(countryIds.Count)];
        }

        if (countryIds.Count > 0)
        {
            car.ManufacturerCountryId = countryIds[random.Next(countryIds.Count)];
        }

        // Стан фарби пов'язаний із ДТП: у битого «заводська фарба» траплялася б
        // рідше, ніж у цілого, і дані не мають цьому суперечити.
        car.PaintCondition = car.WasInAccident
            ? (PaintCondition)random.Next(1, 3)
            : (PaintCondition)random.Next(0, 2);

        car.DamageState = car.WasInAccident && random.Next(4) == 0
            ? DamageState.Damaged
            : DamageState.NotDamaged;

        foreach (var featureId in PickFeatures(random, featureIds))
        {
            car.Features.Add(new CarFeature { FeatureId = featureId });
        }

        return car;
    }

    /// <summary>
    /// Додає оголошенню від одного до трьох фото-заглушок, на яких намальовані
    /// марка, модель і рік — щоб у видачі можна було впізнати авто, не читаючи
    /// підпису.
    /// </summary>
    public static async Task AddPhotosAsync(
        Listing listing,
        string makeName,
        string modelName,
        int year,
        Random random,
        IPhotoStorage storage,
        CancellationToken cancellationToken)
    {
        var count = random.Next(1, 4);

        for (var index = 0; index < count; index++)
        {
            var source = PlaceholderImageFactory.Create(
                makeName,
                modelName,
                year,
                index,
                random.Next());

            // Проганяємо заглушку тим самим конвеєром, що й справжнє
            // завантаження: демо-дані мають лежати в сховищі так само, як
            // усе інше, разом із мініатюрами.
            using var buffer = new MemoryStream(source);
            var (full, thumbnail) = await ImageProcessor.ProcessAsync(buffer, cancellationToken);

            var directory = "demo";
            var name = Guid.CreateVersion7().ToString("n");

            listing.Car.Photos.Add(new CarPhoto
            {
                Path = await storage.SaveAsync(directory, $"{name}.jpg", full, cancellationToken),
                ThumbnailPath = await storage.SaveAsync(directory, $"{name}-thumb.jpg", thumbnail, cancellationToken),
                SortOrder = index,
                IsPrimary = index == 0,
            });
        }
    }

    /// <summary>
    /// Тип пального з правдоподібними частками: бензин і дизель складають
    /// більшість, електро лишається рідкістю — як і на справжньому ринку.
    /// </summary>
    public static FuelType PickFuelType(Random random) => random.Next(100) switch
    {
        < 45 => FuelType.Petrol,
        < 75 => FuelType.Diesel,
        < 84 => FuelType.PetrolGas,
        < 92 => FuelType.Hybrid,
        < 96 => FuelType.PluginHybrid,
        _ => FuelType.Electric,
    };

    private static IEnumerable<long> PickFeatures(Random random, List<long> featureIds)
    {
        if (featureIds.Count == 0)
        {
            yield break;
        }

        var wanted = random.Next(3, 12);
        var chosen = new HashSet<long>();

        while (chosen.Count < wanted && chosen.Count < featureIds.Count)
        {
            chosen.Add(featureIds[random.Next(featureIds.Count)]);
        }

        foreach (var featureId in chosen)
        {
            yield return featureId;
        }
    }
}
