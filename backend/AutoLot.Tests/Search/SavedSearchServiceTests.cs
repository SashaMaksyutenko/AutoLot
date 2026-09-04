using System.Globalization;
using AutoLot.Application.Catalog;
using AutoLot.Application.Search;
using AutoLot.Domain.Cars;
using AutoLot.Domain.Common;
using AutoLot.Domain.Enums;
using AutoLot.Domain.Geo;
using AutoLot.Domain.Identity;
using AutoLot.Domain.Listings;
using AutoLot.Domain.Search;
using AutoLot.Infrastructure.Catalog;
using AutoLot.Infrastructure.Geo;
using AutoLot.Infrastructure.Listings;
using AutoLot.Infrastructure.Persistence;
using AutoLot.Infrastructure.Search;
using AutoLot.Tests.TestDoubles;
using Microsoft.EntityFrameworkCore;

namespace AutoLot.Tests.Search;

/// <summary>
/// Збережені пошуки.
///
/// Головне тут — що зберігається НАБІР ФІЛЬТРІВ, а не знайдене: кількість
/// збігів рахується щоразу заново, тож новий автомобіль потрапляє в старий
/// пошук сам.
/// </summary>
public class SavedSearchServiceTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);

    private const long OwnerId = 1;

    private const long StrangerId = 2;

    private readonly TestDatabase database = new();

    private readonly AutoLotDbContext context;

    public SavedSearchServiceTests()
    {
        context = database.CreateContext();
        Seed();
    }

    [Fact]
    public async Task A_search_keeps_its_filters()
    {
        var query = new CatalogQuery { MakeId = 1, YearFrom = 2018, PriceTo = 12_000m };

        var saved = await Service().SaveAsync(OwnerId, "Свіжі BMW", query);

        Assert.Equal("Свіжі BMW", saved.Name);
        Assert.Equal(1, saved.Query.MakeId);
        Assert.Equal(2018, saved.Query.YearFrom);
        Assert.Equal(12_000m, saved.Query.PriceTo);
    }

    [Fact]
    public async Task Arrays_of_filters_survive_the_round_trip()
    {
        var query = new CatalogQuery
        {
            BodyTypes = [BodyType.Universal, BodyType.Crossover],
            FuelTypes = [FuelType.Diesel],
        };

        var saved = await Service().SaveAsync(OwnerId, "Дизельні універсали", query);

        // Набори — найлегше місце, де JSON-перетворення губить дані.
        Assert.Equal([BodyType.Universal, BodyType.Crossover], saved.Query.BodyTypes);
        Assert.Equal([FuelType.Diesel], saved.Query.FuelTypes);
    }

    [Fact]
    public async Task The_page_number_is_not_part_of_a_search()
    {
        var saved = await Service().SaveAsync(
            OwnerId,
            "Будь-що",
            new CatalogQuery { Page = 7 });

        // Збережений пошук описує, ЩО шукати, а не де людина зупинилася гортати.
        Assert.Equal(1, saved.Query.Page);
    }

    [Fact]
    public async Task The_match_count_is_counted_now_not_at_saving_time()
    {
        NewListing(makeId: 1, year: 2020);

        var saved = await Service().SaveAsync(OwnerId, "BMW", new CatalogQuery { MakeId = 1 });
        Assert.Equal(1, saved.MatchCount);

        // Нове авто з'явилося вже після збереження.
        NewListing(makeId: 1, year: 2021);

        var mine = await Service().GetMineAsync(OwnerId);
        Assert.Equal(2, mine[0].MatchCount);
    }

    [Fact]
    public async Task The_count_respects_the_saved_filters()
    {
        NewListing(makeId: 1, year: 2020);
        NewListing(makeId: 2, year: 2020);

        var saved = await Service().SaveAsync(OwnerId, "Лише BMW", new CatalogQuery { MakeId = 1 });

        Assert.Equal(1, saved.MatchCount);
    }

    [Fact]
    public async Task Drafts_never_count_as_matches()
    {
        NewListing(makeId: 1, year: 2020, ListingStatus.Draft);

        var saved = await Service().SaveAsync(OwnerId, "BMW", new CatalogQuery { MakeId = 1 });

        // Каталог показує лише опубліковане, і число має збігатися з тим,
        // що людина побачить, відкривши цей пошук.
        Assert.Equal(0, saved.MatchCount);
    }

    [Fact]
    public async Task The_newest_search_comes_first()
    {
        await ServiceAt(Now).SaveAsync(OwnerId, "Перший", new CatalogQuery());
        await ServiceAt(Now.AddMinutes(5)).SaveAsync(OwnerId, "Другий", new CatalogQuery());

        var mine = await Service().GetMineAsync(OwnerId);

        Assert.Equal("Другий", mine[0].Name);
        Assert.Equal("Перший", mine[1].Name);
    }

    [Fact]
    public async Task Only_my_searches_are_mine()
    {
        await Service().SaveAsync(OwnerId, "Мій", new CatalogQuery());
        await Service().SaveAsync(StrangerId, "Чужий", new CatalogQuery());

        var mine = await Service().GetMineAsync(OwnerId);

        Assert.Equal("Мій", Assert.Single(mine).Name);
    }

    [Fact]
    public async Task A_search_can_be_renamed()
    {
        var saved = await Service().SaveAsync(OwnerId, "Стара назва", new CatalogQuery { MakeId = 1 });

        var renamed = await Service().RenameAsync(saved.Id, OwnerId, "  Нова назва  ");

        // Пробіли по краях зрізаються: інакше в списку були б назви, що
        // виглядають однаково, але не рівні.
        Assert.Equal("Нова назва", renamed.Name);
        Assert.Equal(1, renamed.Query.MakeId);
    }

    [Fact]
    public async Task A_blank_name_is_refused()
    {
        var saved = await Service().SaveAsync(OwnerId, "Назва", new CatalogQuery());

        await Assert.ThrowsAsync<DomainRuleException>(
            () => Service().RenameAsync(saved.Id, OwnerId, "   "));
    }

    [Fact]
    public async Task A_stranger_cannot_touch_someone_elses_search()
    {
        var saved = await Service().SaveAsync(OwnerId, "Мій", new CatalogQuery());

        // Саме «не знайдено», а не «немає доступу»: скільки пошуків зберіг
        // сусід — не справа стороннього.
        await Assert.ThrowsAsync<SavedSearchNotFoundException>(
            () => Service().RenameAsync(saved.Id, StrangerId, "Захоплено"));

        await Assert.ThrowsAsync<SavedSearchNotFoundException>(
            () => Service().DeleteAsync(saved.Id, StrangerId));
    }

    [Fact]
    public async Task A_search_can_be_deleted()
    {
        var saved = await Service().SaveAsync(OwnerId, "Тимчасовий", new CatalogQuery());

        await Service().DeleteAsync(saved.Id, OwnerId);

        Assert.Empty(await Service().GetMineAsync(OwnerId));
    }

    [Fact]
    public async Task The_number_of_searches_is_capped()
    {
        for (var i = 0; i < SavedSearch.PerUserLimit; i++)
        {
            await Service().SaveAsync(OwnerId, $"Пошук {i}", new CatalogQuery());
        }

        var refused = await Assert.ThrowsAsync<DomainRuleException>(
            () => Service().SaveAsync(OwnerId, "Ще один", new CatalogQuery()));

        Assert.Contains(
            SavedSearch.PerUserLimit.ToString(CultureInfo.InvariantCulture),
            refused.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Broken_json_hides_nothing_else()
    {
        await Service().SaveAsync(OwnerId, "Цілий", new CatalogQuery { MakeId = 1 });

        context.SavedSearches.Add(new SavedSearch
        {
            UserId = OwnerId,
            Name = "Зіпсований",
            QueryJson = "{ це не json",
            CreatedAt = Now.AddMinutes(1),
        });

        await context.SaveChangesAsync();

        var mine = await Service().GetMineAsync(OwnerId);

        // Одна зламана стрічка не має ховати від людини решту її пошуків:
        // зіпсоване читається як порожній запит.
        Assert.Equal(2, mine.Count);
        Assert.Null(mine[0].Query.MakeId);
        Assert.Equal(1, mine[1].Query.MakeId);
    }

    public void Dispose()
    {
        context.Dispose();
        database.Dispose();
        GC.SuppressFinalize(this);
    }

    private SavedSearchService Service() => ServiceAt(Now);

    private SavedSearchService ServiceAt(DateTimeOffset now)
    {
        var language = new StubLanguage();

        var mapper = new ListingMapper(
            context,
            language,
            new StubCurrentUser(OwnerId),
            new GeoCatalog(context, language));

        return new SavedSearchService(
            context,
            new FixedClock(now),
            new CatalogService(context, new StubExchangeRates(), mapper));
    }

    private void NewListing(long makeId, int year, ListingStatus status = ListingStatus.Active)
    {
        context.Listings.Add(new Listing
        {
            Title = "Тестовий лот",
            Description = "Опис",
            SellerId = StrangerId,
            CityId = 1,
            Price = 10_000m,
            Currency = Currency.Usd,
            PriceUah = 420_000m,
            Status = status,
            Car = new Car
            {
                Year = year,
                MakeId = makeId,
                ModelId = makeId,
                FuelType = FuelType.Diesel,
                Transmission = TransmissionType.Manual,
                Drivetrain = DrivetrainType.FrontWheel,
                BodyType = BodyType.Universal,
                Color = CarColor.Black,
            },
        });

        context.SaveChanges();
    }

    private void Seed()
    {
        context.Regions.Add(new Region { Id = 1, Code = "kyiv-region" });
        context.Cities.Add(new City { Id = 1, RegionId = 1, Code = "kyiv" });
        context.Makes.Add(new Make { Id = 1, Name = "BMW", Slug = "bmw" });
        context.Makes.Add(new Make { Id = 2, Name = "Audi", Slug = "audi" });
        context.Models.Add(new Model { Id = 1, MakeId = 1, Name = "X5", Slug = "x5" });
        context.Models.Add(new Model { Id = 2, MakeId = 2, Name = "A4", Slug = "a4" });

        context.Users.Add(NewUser(OwnerId, "owner", "Власник"));
        context.Users.Add(NewUser(StrangerId, "stranger", "Сторонній"));

        context.SaveChanges();
    }

    private static User NewUser(long id, string login, string displayName) => new()
    {
        Id = id,
        UserName = $"{login}@example.com",
        Email = $"{login}@example.com",
        DisplayName = displayName,
    };
}
