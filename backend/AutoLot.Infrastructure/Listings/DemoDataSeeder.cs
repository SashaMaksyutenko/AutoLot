using AutoLot.Application.Common.Abstractions;
using AutoLot.Domain.Auctions;
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

        // Райони є лише у великих містах, тож тримаємо їх за містом:
        // приписати оголошенню чужий район означало б зламати вибір у формі.
        var districtsByCity = await dbContext.CityDistricts
            .GroupBy(district => district.CityId)
            .ToDictionaryAsync(
                group => group.Key,
                group => group.Select(district => district.Id).ToList(),
                cancellationToken);

        if (sellers.Count == 0 || models.Count == 0 || cities.Count == 0)
        {
            LogSkipped(logger);
            return;
        }

        var now = clock.UtcNow;
        var created = 0;
        var popular = PickPopularCombinations(random, models, now);

        for (var index = 0; index < settings.ListingCount; index++)
        {
            // Більшість оголошень — на ходові моделі, решта розсіяна по всіх
            // інших. Так виглядає справжній майданчик: кілька моделей займають
            // половину видачі, а далі йде довгий хвіст поодиноких авто.
            //
            // Це не косметика. Рівномірний випадок по чотириста моделях давав
            // менше половини оголошення на модель, а отже — жодної вибірки,
            // на якій можна порахувати ринкову ціну.
            var (model, year) = random.NextDouble() < PopularShare
                ? popular[random.Next(popular.Count)]
                : (models[random.Next(models.Count)], random.Next(2005, now.Year + 1));

            var listing = await BuildListingAsync(
                random,
                sellers[random.Next(sellers.Count)],
                model,
                year,
                cities[random.Next(cities.Count)],
                districtsByCity,
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
        int year,
        CityRow city,
        Dictionary<long, List<long>> districtsByCity,
        List<long> featureIds,
        List<long> countryIds,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var isNew = year >= now.Year && random.Next(10) == 0;
        var fuelType = PickFuelType(random);
        var currency = random.Next(4) == 0 ? Currency.Uah : Currency.Usd;

        // Ціна залежить від віку авто, а не береться навмання. Інакше
        // «медіана по моделі та році» рахувалася б із чисел, між якими
        // немає жодного зв'язку, і довідка про ринок вводила б в оману.
        var age = Math.Max(0, now.Year - year);
        var baseUsd = Math.Max(1_500, 42_000 - (age * 2_400));
        var spread = random.Next(-15, 16) / 100m;
        var usd = decimal.Round(baseUsd * (1 + spread), 0);

        var price = currency is Currency.Uah
            ? decimal.Round(usd * 42m, 0)
            : usd;

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
            // Приблизно кожне восьме — лот з торгами: у видачі має бути видно
            // обидва типи. Самі торги створюються нижче, разом з оголошенням.
            Type = random.Next(8) == 0 ? ListingType.Auction : ListingType.FixedPrice,
            Status = ListingStatus.Active,
            PublishedAt = now.AddDays(-random.Next(0, 45)),
            ExpiresAt = now.AddDays(random.Next(15, 60)),
            IsNegotiable = random.Next(3) == 0,
            AcceptsTrade = random.Next(4) == 0,
            IsUrgent = random.Next(8) == 0,
            Car = BuildCar(random, model, year, isNew, fuelType, featureIds, countryIds),
        };

        // Район ставимо не завжди: у житті його вказує приблизно кожен другий.
        if (districtsByCity.TryGetValue(city.Id, out var districts)
            && districts.Count > 0
            && random.Next(2) == 0)
        {
            listing.CityDistrictId = districts[random.Next(districts.Count)];
        }

        await AddPhotosAsync(listing, model, year, random, cancellationToken);

        AddAuctionIfNeeded(listing, random, now);

        return listing;
    }

    /// <summary>
    /// Демонстраційним лотам потрібні справжні торги, інакше сторінка лота
    /// відкривалася б порожньою. У житті аукціон стартує при схваленні
    /// модератором, але демо-дані модерацію оминають, тож створюємо тут.
    ///
    /// Строк розкидаємо: частина лотів має завершитися за кілька годин,
    /// частина — за дні. Так на сторінці видно і майже дотліле, і свіже.
    /// </summary>
    private void AddAuctionIfNeeded(Listing listing, Random random, DateTimeOffset now)
    {
        if (listing.Type != ListingType.Auction)
        {
            return;
        }

        listing.ReservePrice = random.Next(3) == 0
            ? decimal.Round(listing.Price * 1.2m, 2)
            : null;

        dbContext.Auctions.Add(new Auction
        {
            Listing = listing,
            Currency = listing.Currency,
            StartPrice = listing.Price,
            CurrentPrice = listing.Price,
            ReservePrice = listing.ReservePrice,
            StartsAt = now,
            EndsAt = now.AddHours(random.Next(3, 24 * 7)),
            Status = AuctionStatus.Active,
        });
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

    /// <summary>
    /// Частка оголошень, що припадає на ходові моделі.
    /// </summary>
    /// <remarks>
    /// Дві третини — приблизно так виглядає справжній класифайд: десяток
    /// моделей займає більшу частину видачі. Решта третина лишається на
    /// довгий хвіст, щоб каталог не звівся до десяти назв.
    /// </remarks>
    private const double PopularShare = 0.66;

    /// <summary>
    /// Скільки пар «модель + рік» вважати ходовими. Розрахунок простий:
    /// двісті оголошень, дві третини з них на ці пари — щоб у кожній
    /// набралося помітно більше за поріг ринкової статистики.
    /// </summary>
    private const int PopularCombinations = 16;

    /// <summary>
    /// Обирає ходові пари «модель + рік». Роки беремо свіжі: саме такі авто
    /// й складають більшість оголошень на майданчику.
    /// </summary>
    private static List<(ModelRow Model, int Year)> PickPopularCombinations(
        Random random,
        List<ModelRow> models,
        DateTimeOffset now)
    {
        var combinations = new List<(ModelRow, int)>(PopularCombinations);
        var used = new HashSet<(long, int)>();

        while (combinations.Count < PopularCombinations && used.Count < models.Count * 8)
        {
            var model = models[random.Next(models.Count)];
            var year = now.Year - random.Next(0, 8);

            if (used.Add((model.Id, year)))
            {
                combinations.Add((model, year));
            }
        }

        return combinations;
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
