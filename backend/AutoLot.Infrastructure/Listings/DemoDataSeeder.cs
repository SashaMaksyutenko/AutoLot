using AutoLot.Application.Common.Abstractions;
using AutoLot.Domain.Cars;
using AutoLot.Domain.Enums;
using AutoLot.Domain.Geo;
using AutoLot.Domain.Identity;
using AutoLot.Domain.Listings;
using AutoLot.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutoLot.Infrastructure.Listings;

/// <summary>
/// Наповнює базу демонстраційними оголошеннями (SPEC §11). Вимикається
/// налаштуванням і за замовчуванням не працює: у робочому середовищі вигадані
/// оголошення нікому не потрібні.
///
/// Дані генеруються з фіксованим зерном, тож кожен запуск дає той самий набір —
/// зручно, коли треба відтворити побачене.
/// </summary>
public sealed partial class DemoDataSeeder(
    AutoLotDbContext dbContext,
    UserManager<User> userManager,
    IPhotoStorage storage,
    IExchangeRateProvider exchangeRates,
    IDateTimeProvider clock,
    IOptions<DemoDataOptions> options,
    ILogger<DemoDataSeeder> logger) : IDataSeeder
{
    private readonly DemoDataOptions settings = options.Value;

    /// <summary>Останній: потребує і довідників, і ролей.</summary>
    public int Order => 100;

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!settings.Enabled)
        {
            return;
        }

        // Ідемпотентність тут груба, але доречна: якщо оголошення вже є,
        // другий набір вигаданих лише заважатиме.
        if (await dbContext.Listings.AnyAsync(cancellationToken))
        {
            return;
        }

        var random = new Random(settings.Seed);

        var sellers = await CreateSellersAsync(random, cancellationToken);
        var models = await LoadModelsAsync(cancellationToken);
        var cities = await LoadCitiesAsync(cancellationToken);
        var featureIds = await dbContext.Features.Select(feature => feature.Id).ToListAsync(cancellationToken);
        var countryIds = await dbContext.Countries.Select(country => country.Id).ToListAsync(cancellationToken);

        if (sellers.Count == 0 || models.Count == 0 || cities.Count == 0)
        {
            LogSkipped(logger);
            return;
        }

        var now = clock.UtcNow;
        var created = 0;

        for (var index = 0; index < settings.ListingCount; index++)
        {
            var listing = await BuildListingAsync(
                random,
                sellers[random.Next(sellers.Count)],
                models[random.Next(models.Count)],
                cities[random.Next(cities.Count)],
                featureIds,
                countryIds,
                now,
                cancellationToken);

            dbContext.Listings.Add(listing);
            created++;

            // Зберігаємо порціями: 200 оголошень із фото одним SaveChanges
            // тримали б у пам'яті надто багато відстежуваних сутностей.
            if (created % 25 == 0)
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        LogSeeded(logger, created, sellers.Count);
    }

    private async Task<Listing> BuildListingAsync(
        Random random,
        User seller,
        ModelRow model,
        CityRow city,
        List<long> featureIds,
        List<long> countryIds,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var year = random.Next(2005, now.Year + 1);
        var isNew = year >= now.Year && random.Next(10) == 0;
        var fuelType = PickFuelType(random);
        var currency = random.Next(4) == 0 ? Currency.Uah : Currency.Usd;

        var price = currency is Currency.Uah
            ? random.Next(150, 3000) * 1000m
            : random.Next(2, 75) * 1000m;

        var listing = new Listing
        {
            Title = $"{model.MakeName} {model.Name} {year}",
            Description =
                $"{model.MakeName} {model.Name} {year} року. Технічний стан справний, " +
                "обслуговування за регламентом. Демонстраційне оголошення, згенероване для наповнення каталогу.",
            SellerId = seller.Id,
            CityId = city.Id,
            Price = price,
            Currency = currency,
            PriceUah = decimal.Round(
                price * await exchangeRates.GetRateToUahAsync(currency, cancellationToken),
                2),
            Type = ListingType.FixedPrice,
            Status = ListingStatus.Active,
            PublishedAt = now.AddDays(-random.Next(0, 45)),
            ExpiresAt = now.AddDays(random.Next(15, 60)),
            IsNegotiable = random.Next(3) == 0,
            AcceptsTrade = random.Next(4) == 0,
            IsUrgent = random.Next(8) == 0,
            Car = BuildCar(random, model, year, isNew, fuelType, featureIds, countryIds),
        };

        await AddPhotosAsync(listing, model, year, random, cancellationToken);

        return listing;
    }

    private static Car BuildCar(
        Random random,
        ModelRow model,
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
            MakeId = model.MakeId,
            ModelId = model.Id,
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

        foreach (var featureId in PickFeatures(random, featureIds))
        {
            car.Features.Add(new CarFeature { FeatureId = featureId });
        }

        return car;
    }

    private async Task AddPhotosAsync(
        Listing listing,
        ModelRow model,
        int year,
        Random random,
        CancellationToken cancellationToken)
    {
        var count = random.Next(1, 4);

        for (var index = 0; index < count; index++)
        {
            var source = PlaceholderImageFactory.Create(
                model.MakeName,
                model.Name,
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

    private static FuelType PickFuelType(Random random) => random.Next(100) switch
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

    private async Task<List<User>> CreateSellersAsync(Random random, CancellationToken cancellationToken)
    {
        var sellers = new List<User>();

        for (var index = 1; index <= settings.SellerCount; index++)
        {
            var email = $"demo{index}@autolot.local";
            var existing = await userManager.FindByEmailAsync(email);

            if (existing is not null)
            {
                sellers.Add(existing);
                continue;
            }

            // Кожен третій — дилер: у видачі має бути видно обидва типи продавця.
            var isDealer = index % 3 == 0;

            var user = new User
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = isDealer ? $"Автосалон №{index}" : $"Продавець {index}",
                AccountType = isDealer ? AccountType.Dealer : AccountType.Private,
            };

            var created = await userManager.CreateAsync(user, settings.SellerPassword);

            if (!created.Succeeded)
            {
                continue;
            }

            await userManager.AddToRoleAsync(user, RoleNames.User);
            sellers.Add(user);
        }

        _ = random;

        return sellers;
    }

    private Task<List<ModelRow>> LoadModelsAsync(CancellationToken cancellationToken) =>
        dbContext.Models
            .AsNoTracking()
            .Select(model => new ModelRow(model.Id, model.Name, model.MakeId, model.Make.Name))
            .ToListAsync(cancellationToken);

    private Task<List<CityRow>> LoadCitiesAsync(CancellationToken cancellationToken) =>
        dbContext.Cities
            .AsNoTracking()

            // Беремо помітні міста: демо-оголошення в селі з населенням 300
            // виглядало б дивно поруч із реальною видачею.
            .Where(city => city.Population >= 50_000)
            .Select(city => new CityRow(city.Id))
            .ToListAsync(cancellationToken);

    private sealed record ModelRow(long Id, string Name, long MakeId, string MakeName);

    private sealed record CityRow(long Id);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Демо-дані: {Listings} оголошень від {Sellers} продавців")]
    private static partial void LogSeeded(ILogger logger, int listings, int sellers);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Демо-дані не створено: бракує довідників або продавців")]
    private static partial void LogSkipped(ILogger logger);
}
