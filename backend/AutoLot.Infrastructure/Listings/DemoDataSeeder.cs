using AutoLot.Application.Common.Abstractions;
using AutoLot.Domain.Auctions;
using AutoLot.Domain.Cars;
using AutoLot.Domain.Dealers;
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
///
/// Працює у два режими. Порожня база — створює все з нуля. Наповнена — нічого
/// не додає, але **приводить наявне до ладу**: оживляє торги, яким вийшов
/// строк, і доробляє те, чого в базі ще не було. Другий режим потрібен тому,
/// що демо-дані псуються від часу: лот, засіяний місяць тому, давно закритий,
/// і вкладка «Аукціони» зустрічає відвідувача порожнечею.
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

    private const string ResourceName = "AutoLot.Infrastructure.Persistence.SeedData.demo-sellers.json";

    private const string WmiResourceName = "AutoLot.Infrastructure.Persistence.SeedData.demo-wmi.json";

    /// <summary>
    /// Спільний хвіст пошти всіх вигаданих продавців. За ним і тільки за ним
    /// сідер упізнає свої дані: усе інше в базі могла створити людина руками,
    /// і чіпати це ми не маємо права.
    /// </summary>
    private const string EmailDomain = "@autolot.local";

    /// <summary>Останній: потребує і довідників, і ролей.</summary>
    public int Order => 100;

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!settings.Enabled)
        {
            return;
        }

        var document = await SeedResource.ReadAsync<DemoSellersDocument>(ResourceName, cancellationToken);
        var cities = await LoadCitiesAsync(cancellationToken);

        if (document.Sellers.Count == 0 || cities.Count == 0)
        {
            LogSkipped(logger);
            return;
        }

        var sellers = await EnsureSellersAsync(document.Sellers, cancellationToken);

        if (sellers.Count == 0)
        {
            LogSkipped(logger);
            return;
        }

        var random = new Random(settings.Seed);

        await EnsureDealershipsAsync(sellers, cities, random, cancellationToken);

        // Ідемпотентність тут груба, але доречна: якщо оголошення вже є,
        // другий набір вигаданих лише заважатиме.
        if (await dbContext.Listings.AnyAsync(cancellationToken))
        {
            await RefreshAsync(sellers, random, cancellationToken);
            return;
        }

        await CreateListingsAsync(sellers, cities, random, cancellationToken);
    }

    // ─────────────────────────── Продавці й салони ───────────────────────────

    /// <summary>
    /// Зводить записи з файла з акаунтами в базі: кого немає — створює, у кого
    /// розійшлося ім'я чи тип акаунта — виправляє.
    ///
    /// Виправляє навмисно. Файл тут джерело істини, і той, хто заповнював базу
    /// торішньою версією файла, має отримати нинішні імена без видалення бази.
    /// </summary>
    private async Task<List<DemoSeller>> EnsureSellersAsync(
        List<DemoSellerRow> rows,
        CancellationToken cancellationToken)
    {
        var sellers = new List<DemoSeller>(rows.Count);

        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.Email) || string.IsNullOrWhiteSpace(row.Name))
            {
                continue;
            }

            var email = row.Email + EmailDomain;
            var accountType = row.Dealership is null ? AccountType.Private : AccountType.Dealer;
            var user = await userManager.FindByEmailAsync(email);

            if (user is null)
            {
                user = new User
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    DisplayName = row.Name,
                    AccountType = accountType,
                };

                if (!(await userManager.CreateAsync(user, settings.SellerPassword)).Succeeded)
                {
                    continue;
                }

                await userManager.AddToRoleAsync(user, RoleNames.User);
            }
            else if (user.DisplayName != row.Name || user.AccountType != accountType)
            {
                user.DisplayName = row.Name;
                user.AccountType = accountType;

                await userManager.UpdateAsync(user);
            }

            sellers.Add(new DemoSeller(user, row.Dealership));
        }

        return sellers;
    }

    /// <summary>
    /// Створює автосалони для тих продавців, у кого в файлі описаний салон, і
    /// записує їх власниками.
    ///
    /// Навіщо це взагалі. Тип акаунта «дилер» сам собою нічого не означає:
    /// бейдж у видачі бере назву з САЛОНУ, і без нього оголошення дилера
    /// підписувалося «приватна особа» — тобто демо-дані суперечили самі собі.
    /// </summary>
    private async Task EnsureDealershipsAsync(
        List<DemoSeller> sellers,
        List<CityRow> cities,
        Random random,
        CancellationToken cancellationToken)
    {
        var owners = sellers.Where(seller => seller.Row is not null).ToList();

        if (owners.Count == 0)
        {
            return;
        }

        var slugs = owners.Select(owner => owner.Row!.Slug).ToList();

        var existing = await dbContext.Dealerships
            .Where(dealership => slugs.Contains(dealership.Slug))
            .ToDictionaryAsync(dealership => dealership.Slug, cancellationToken);

        var now = clock.UtcNow;
        var created = 0;

        foreach (var owner in owners)
        {
            var row = owner.Row!;

            if (existing.TryGetValue(row.Slug, out var found))
            {
                owner.DealershipId = found.Id;
                continue;
            }

            var dealership = new Dealership
            {
                Name = row.Name,
                Slug = row.Slug,
                Description = row.Description,
                CityId = cities[random.Next(cities.Count)].Id,
            };

            /*
                Перевірені й неперевірені разом: бейдж має бути видно, але й
                салон без нього теж має траплятися, інакше різниці не побачити.

                Поля виставляємо напряму, а не методом Verify: він вимагає
                вказати модератора, який перевірив, а тут такого немає. Записати
                туди самого власника означало б підробити слід у журналі рішень.
            */
            dealership.IsVerified = row.IsVerified;
            dealership.VerifiedAt = row.IsVerified ? now : null;

            dealership.Members.Add(new DealershipMember
            {
                User = owner.User,
                Role = DealershipRole.Owner,
                JoinedAt = now,
            });

            dbContext.Dealerships.Add(dealership);
            created++;
        }

        if (created == 0)
        {
            return;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        // Ідентифікатори відомі лише після збереження — саме тому другий прохід.
        foreach (var owner in owners.Where(item => item.DealershipId is null))
        {
            owner.DealershipId = await dbContext.Dealerships
                .Where(dealership => dealership.Slug == owner.Row!.Slug)
                .Select(dealership => (long?)dealership.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        LogDealerships(logger, created);
    }

    // ─────────────────────────── Наповнена база ───────────────────────────

    /// <summary>
    /// Приводить до ладу дані, які вже лежать у базі: прив'язує оголошення до
    /// салонів, яких на момент сіду ще не існувало, і перезапускає торги, що
    /// встигли завершитися.
    /// </summary>
    private async Task RefreshAsync(
        List<DemoSeller> sellers,
        Random random,
        CancellationToken cancellationToken)
    {
        var linked = await LinkListingsToDealershipsAsync(sellers, cancellationToken);
        var restarted = await RestartStaleAuctionsAsync(sellers, random, cancellationToken);
        var numbered = await BackfillVinsAsync(sellers, random, cancellationToken);
        var rounded = await RoundDemoPricesAsync(sellers, cancellationToken);
        var priced = await BackfillPriceHistoryAsync(sellers, random, cancellationToken);

        if (linked > 0 || restarted > 0 || numbered > 0 || rounded > 0 || priced > 0)
        {
            LogRefreshed(logger, restarted, linked, numbered, priced + rounded);
        }
    }

    /// <summary>
    /// Округлює ціни демо-оголошень, засіяних раніше, разом з їхньою історією.
    /// </summary>
    /// <remarks>
    /// Ранні версії сідера ставили ціну з точністю до долара («13 992 $»), а
    /// стару ціну в історії рахували множенням на відсоток («58 015 $»).
    /// Справжні продавці так не пишуть, і вигадані дані видавали себе з
    /// першого погляду.
    ///
    /// Записи історії загалом не редагує ніхто; це правило стосується
    /// справжніх змін ціни. Ці ж записи вигадав сам сідер і лише для своїх,
    /// демонстраційних оголошень. Правимо їх НА МІСЦІ, а не видаляємо: ті
    /// самі авто лишаються з історією, з тією самою кількістю змін і тими
    /// самими датами — див. DemoPrices.RoundInPlace.
    ///
    /// Після одного проходу все кругле, тож наступні запуски нічого не чіпають.
    /// Лоти з торгами пропускаємо: їхня ціна прив'язана до стартової ціни
    /// аукціону, і правити одне без іншого означало б розсинхронізувати їх.
    /// </remarks>
    private async Task<int> RoundDemoPricesAsync(
        List<DemoSeller> sellers,
        CancellationToken cancellationToken)
    {
        var sellerIds = IdsOf(sellers);

        var listings = await dbContext.Listings
            .Where(listing => sellerIds.Contains(listing.SellerId) && listing.Type == ListingType.FixedPrice)
            .ToListAsync(cancellationToken);

        var listingIds = listings.Select(listing => listing.Id).ToList();

        var histories = (await dbContext.Set<PriceChange>()
                .Where(change => listingIds.Contains(change.ListingId))
                .OrderBy(change => change.ChangedAt)
                .ThenBy(change => change.Id)
                .ToListAsync(cancellationToken))
            .GroupBy(change => change.ListingId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var rounded = 0;

        foreach (var listing in listings)
        {
            var history = histories.GetValueOrDefault(listing.Id) ?? [];

            var isRound = listing.Price == DemoPrices.Round(listing.Price, listing.Currency)
                && history.TrueForAll(point => point.Price == DemoPrices.Round(point.Price, point.Currency));

            if (isRound)
            {
                continue;
            }

            var rate = await exchangeRates.GetRateToUahAsync(listing.Currency, cancellationToken);

            listing.Price = DemoPrices.Round(listing.Price, listing.Currency);
            listing.PriceUah = decimal.Round(listing.Price * rate, 2);

            DemoPrices.RoundInPlace(history, listing.Price, listing.Currency, rate);

            rounded++;
        }

        if (rounded > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return rounded;
    }

    /// <summary>
    /// Вигадує історію ціни тим демо-оголошенням, у яких її ще немає.
    ///
    /// Та сама причина, що й у номерів кузова: база, засіяна раніше, лишилася
    /// б без жодного графіка, і побачити нову можливість можна було б хіба що
    /// стерши всі дані.
    /// </summary>
    private async Task<int> BackfillPriceHistoryAsync(
        List<DemoSeller> sellers,
        Random random,
        CancellationToken cancellationToken)
    {
        var sellerIds = IdsOf(sellers);
        var now = clock.UtcNow;

        // Беремо лише ті, де історії немає зовсім: дописувати до наявної
        // означало б плутати вигадане зі справжнім.
        var listings = await dbContext.Listings
            .Where(listing => sellerIds.Contains(listing.SellerId))
            .Where(listing => !dbContext.Set<PriceChange>()
                .Any(change => change.ListingId == listing.Id))
            .ToListAsync(cancellationToken);

        if (listings.Count == 0)
        {
            return 0;
        }

        foreach (var listing in listings)
        {
            dbContext.Set<PriceChange>().AddRange(
                DemoPrices.Create(listing, random, listing.PublishedAt ?? now, now));
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return listings.Count;
    }

    /// <summary>
    /// Дописує номери кузова тим демо-авто, у яких їх ще немає.
    ///
    /// Потрібно рівно з тієї ж причини, що й перезапуск торгів: база, засіяна
    /// раніше, лишилася б із порожнім полем VIN на кожній картці, і побачити
    /// нову перевірку можна було б хіба що стерши всі дані.
    ///
    /// Чужого не чіпаємо: беремо лише авто продавців із сід-файла, і лише ті,
    /// де номера справді немає.
    /// </summary>
    private async Task<int> BackfillVinsAsync(
        List<DemoSeller> sellers,
        Random random,
        CancellationToken cancellationToken)
    {
        var sellerIds = IdsOf(sellers);

        var cars = await dbContext.Listings
            .Where(listing => sellerIds.Contains(listing.SellerId) && listing.Car.Vin == null)
            .Select(listing => new { listing.Car, Slug = listing.Car.Make.Slug })
            .ToListAsync(cancellationToken);

        if (cars.Count == 0)
        {
            return 0;
        }

        var prefixes = (await SeedResource.ReadAsync<DemoWmiDocument>(WmiResourceName, cancellationToken))
            .Prefixes;

        var numbered = 0;

        foreach (var row in cars)
        {
            var vin = DemoVins.Create(prefixes, row.Slug, row.Car.Year, random);

            if (vin is null)
            {
                continue;
            }

            row.Car.Vin = vin;
            numbered++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return numbered;
    }

    /// <summary>
    /// Дописує оголошенням демо-дилерів їхній салон. Чужі оголошення й ті, де
    /// салон уже проставлений, не чіпає.
    /// </summary>
    private async Task<int> LinkListingsToDealershipsAsync(
        List<DemoSeller> sellers,
        CancellationToken cancellationToken)
    {
        var changed = 0;

        foreach (var seller in sellers.Where(item => item.DealershipId is not null))
        {
            changed += await dbContext.Listings
                .Where(listing => listing.SellerId == seller.User.Id && listing.DealershipId == null)
                .ExecuteUpdateAsync(
                    update => update.SetProperty(listing => listing.DealershipId, seller.DealershipId),
                    cancellationToken);
        }

        return changed;
    }

    /// <summary>
    /// Знаходить демо-торги, яким вийшов час, і запускає їх наново — самі
    /// правила перезапуску в <see cref="DemoAuctions.Restart"/>.
    ///
    /// Відбір навмисно вузький: беремо лише лоти продавців із сід-файла. Чужий
    /// аукціон, який хтось створив руками, чіпати не можна — з боку бази він
    /// виглядає так само, а для людини це була б утрачена робота.
    /// </summary>
    private async Task<int> RestartStaleAuctionsAsync(
        List<DemoSeller> sellers,
        Random random,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var sellerIds = IdsOf(sellers);

        var stale = await dbContext.Auctions
            .Include(auction => auction.Listing)
            .Include(auction => auction.Bids)
            .Where(auction => sellerIds.Contains(auction.Listing.SellerId))
            .Where(auction => auction.Status != AuctionStatus.Active || auction.EndsAt <= now)
            .ToListAsync(cancellationToken);

        if (stale.Count == 0)
        {
            return 0;
        }

        foreach (var auction in stale)
        {
            DemoAuctions.Restart(auction, random, now);
            DemoAuctions.AddBids(auction, sellerIds, random, now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return stale.Count;
    }

    // ─────────────────────────── Порожня база ───────────────────────────

    private async Task CreateListingsAsync(
        List<DemoSeller> sellers,
        List<CityRow> cities,
        Random random,
        CancellationToken cancellationToken)
    {
        var models = await LoadModelsAsync(cancellationToken);
        var prefixes = (await SeedResource.ReadAsync<DemoWmiDocument>(WmiResourceName, cancellationToken)).Prefixes;
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

        if (models.Count == 0)
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
                sellers,
                model,
                year,
                prefixes,
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
        DemoSeller seller,
        List<DemoSeller> everyone,
        ModelRow model,
        int year,
        IReadOnlyDictionary<string, string> prefixes,
        CityRow city,
        Dictionary<long, List<long>> districtsByCity,
        List<long> featureIds,
        List<long> countryIds,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var isNew = year >= now.Year && random.Next(10) == 0;
        var fuelType = DemoCars.PickFuelType(random);
        var currency = random.Next(4) == 0 ? Currency.Uah : Currency.Usd;

        // Ціна залежить від віку авто, а не береться навмання. Інакше
        // «медіана по моделі та році» рахувалася б із чисел, між якими
        // немає жодного зв'язку, і довідка про ринок вводила б в оману.
        var age = Math.Max(0, now.Year - year);
        var baseUsd = Math.Max(1_500, 42_000 - (age * 2_400));
        var spread = random.Next(-15, 16) / 100m;
        var usd = decimal.Round(baseUsd * (1 + spread), 0);

        var price = DemoPrices.Round(
            currency is Currency.Uah ? usd * 42m : usd,
            currency);

        var listing = new Listing
        {
            Title = $"{model.MakeName} {model.Name} {year}",
            Description =
                $"{model.MakeName} {model.Name} {year} року. Технічний стан справний, " +
                "обслуговування за регламентом. Демонстраційне оголошення, згенероване для наповнення каталогу.",
            SellerId = seller.User.Id,

            // Оголошення менеджера салону належить салону, а не йому особисто:
            // саме на цьому тримається правило власності (SPEC §10а).
            DealershipId = seller.DealershipId,
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
            Car = DemoCars.Build(
                random,
                model.MakeId,
                model.Id,
                year,
                DemoVins.Create(prefixes, model.MakeSlug, year, random),
                isNew,
                fuelType,
                featureIds,
                countryIds),
        };

        // Район ставимо не завжди: у житті його вказує приблизно кожен другий.
        if (districtsByCity.TryGetValue(city.Id, out var districts)
            && districts.Count > 0
            && random.Next(2) == 0)
        {
            listing.CityDistrictId = districts[random.Next(districts.Count)];
        }

        dbContext.Set<PriceChange>().AddRange(
            DemoPrices.Create(listing, random, listing.PublishedAt ?? now, now));

        await DemoCars.AddPhotosAsync(
            listing,
            model.MakeName,
            model.Name,
            year,
            random,
            storage,
            cancellationToken);

        AddAuctionIfNeeded(listing, everyone, random, now);

        return listing;
    }

    // ─────────────────────────── Торги ───────────────────────────

    /// <summary>
    /// Демонстраційним лотам потрібні справжні торги, інакше сторінка лота
    /// відкривалася б порожньою. У житті аукціон стартує при схваленні
    /// модератором, але демо-дані модерацію оминають, тож створюємо тут.
    /// </summary>
    private void AddAuctionIfNeeded(
        Listing listing,
        List<DemoSeller> everyone,
        Random random,
        DateTimeOffset now)
    {
        if (listing.Type != ListingType.Auction)
        {
            return;
        }

        listing.ReservePrice = random.Next(3) == 0
            ? decimal.Round(listing.Price * 1.2m, 2)
            : null;

        var auction = new Auction
        {
            Listing = listing,
            Currency = listing.Currency,
            StartPrice = listing.Price,
            CurrentPrice = listing.Price,
            ReservePrice = listing.ReservePrice,
            Status = AuctionStatus.Active,
        };

        DemoAuctions.Schedule(auction, random, now);
        DemoAuctions.AddBids(auction, IdsOf(everyone), random, now);

        dbContext.Auctions.Add(auction);
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

    /// <summary>Самі ідентифікатори: правилам торгів решта про продавця не потрібна.</summary>
    private static List<long> IdsOf(List<DemoSeller> sellers) =>
        [.. sellers.Select(seller => seller.User.Id)];

    private Task<List<ModelRow>> LoadModelsAsync(CancellationToken cancellationToken) =>
        dbContext.Models
            .AsNoTracking()
            .Select(model => new ModelRow(
                model.Id,
                model.Name,
                model.MakeId,
                model.Make.Name,
                model.Make.Slug))
            .ToListAsync(cancellationToken);

    private Task<List<CityRow>> LoadCitiesAsync(CancellationToken cancellationToken) =>
        dbContext.Cities
            .AsNoTracking()

            // Беремо помітні міста: демо-оголошення в селі з населенням 300
            // виглядало б дивно поруч із реальною видачею.
            .Where(city => city.Population >= 50_000)
            .Select(city => new CityRow(city.Id))
            .ToListAsync(cancellationToken);

    /// <summary>Продавець із файла разом із його акаунтом і салоном.</summary>
    private sealed class DemoSeller(User user, DemoDealershipRow? row)
    {
        public User User { get; } = user;

        /// <summary>Опис салону з файла; null — звичайна приватна особа.</summary>
        public DemoDealershipRow? Row { get; } = row;

        /// <summary>Заповнюється після того, як салон збережено в базі.</summary>
        public long? DealershipId { get; set; }
    }

    private sealed record ModelRow(long Id, string Name, long MakeId, string MakeName, string MakeSlug);

    private sealed record CityRow(long Id);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Демо-дані: {Listings} оголошень від {Sellers} продавців")]
    private static partial void LogSeeded(ILogger logger, int listings, int sellers);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Демо-дані: створено автосалонів — {Dealerships}")]
    private static partial void LogDealerships(ILogger logger, int dealerships);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Демо-дані оновлено: перезапущено торгів — {Auctions}, прив'язано оголошень до салонів — {Listings}, дописано номерів кузова — {Vins}, історій ціни — {Prices}")]
    private static partial void LogRefreshed(
        ILogger logger,
        int auctions,
        int listings,
        int vins,
        int prices);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Демо-дані не створено: бракує довідників або продавців")]
    private static partial void LogSkipped(ILogger logger);
}
