using AutoLot.Application.Listings;
using AutoLot.Domain.Cars;
using AutoLot.Domain.Enums;
using AutoLot.Domain.Geo;
using AutoLot.Domain.Identity;
using AutoLot.Domain.Listings;
using AutoLot.Infrastructure.Favorites;
using AutoLot.Infrastructure.Geo;
using AutoLot.Infrastructure.Listings;
using AutoLot.Infrastructure.Persistence;
using AutoLot.Tests.TestDoubles;

namespace AutoLot.Tests.Favorites;

/// <summary>
/// Правила обраного перевіряються на справжніх запитах до бази — у пам'яті їх
/// не відтворити: і заборона дублікатів, і відбір за статусом живуть саме в
/// базі, а не в коді сервісу.
/// </summary>
public class FavoriteServiceTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);

    private const long BuyerId = 1;

    private const long SellerId = 2;

    private readonly TestDatabase database = new();

    /// <summary>
    /// Один контекст на весь тест. Це безпечно, бо всі читання в сервісі
    /// йдуть через AsNoTracking: кожен запит іде в базу заново й бачить те,
    /// що туди справді записалося, а не залишки в пам'яті EF.
    /// </summary>
    private readonly AutoLotDbContext context;

    public FavoriteServiceTests()
    {
        context = database.CreateContext();
    }

    [Fact]
    public async Task Adding_a_public_listing_puts_it_in_the_list()
    {
        var listingId = await GivenListing(ListingStatus.Active);

        var added = await Service().AddAsync(BuyerId, listingId);

        Assert.True(added);
        Assert.Equal(1, await Service().CountAsync(BuyerId));
    }

    [Fact]
    public async Task Adding_the_same_listing_twice_changes_nothing()
    {
        var listingId = await GivenListing(ListingStatus.Active);

        await Service().AddAsync(BuyerId, listingId);
        var addedAgain = await Service().AddAsync(BuyerId, listingId);

        // Друге натискання не помилка — просто нічого нового не сталося.
        Assert.False(addedAgain);
        Assert.Equal(1, await Service().CountAsync(BuyerId));
    }

    [Theory]
    [InlineData(ListingStatus.Draft)]
    [InlineData(ListingStatus.PendingModeration)]
    [InlineData(ListingStatus.Rejected)]
    [InlineData(ListingStatus.Archived)]
    public async Task A_listing_that_is_not_public_cannot_be_added(ListingStatus status)
    {
        var listingId = await GivenListing(status);

        // Саме «не знайдено», а не «заборонено»: інакше за кодом відповіді
        // можна було б перевіряти, чи існує чужа чернетка.
        await Assert.ThrowsAsync<ListingNotFoundException>(
            () => Service().AddAsync(BuyerId, listingId));
    }

    [Fact]
    public async Task A_listing_that_does_not_exist_cannot_be_added()
    {
        await Assert.ThrowsAsync<ListingNotFoundException>(
            () => Service().AddAsync(BuyerId, 999_999));
    }

    [Fact]
    public async Task A_sold_listing_stays_in_the_list()
    {
        var listingId = await GivenListing(ListingStatus.Active);
        await Service().AddAsync(BuyerId, listingId);

        await ChangeStatus(listingId, ListingStatus.Sold);

        // Покупцеві корисно побачити, що відкладене авто вже пішло.
        Assert.Equal(1, await Service().CountAsync(BuyerId));
    }

    [Fact]
    public async Task An_archived_listing_disappears_from_the_list()
    {
        var listingId = await GivenListing(ListingStatus.Active);
        await Service().AddAsync(BuyerId, listingId);

        await ChangeStatus(listingId, ListingStatus.Archived);

        Assert.Equal(0, await Service().CountAsync(BuyerId));
    }

    [Fact]
    public async Task An_archived_listing_returns_to_the_list_when_it_is_published_again()
    {
        var listingId = await GivenListing(ListingStatus.Active);
        await Service().AddAsync(BuyerId, listingId);
        await ChangeStatus(listingId, ListingStatus.Archived);

        await ChangeStatus(listingId, ListingStatus.Active);

        // Позначку ми не видаляли — сховали лише саме оголошення.
        Assert.Equal(1, await Service().CountAsync(BuyerId));
    }

    [Fact]
    public async Task Removing_takes_the_listing_out()
    {
        var listingId = await GivenListing(ListingStatus.Active);
        await Service().AddAsync(BuyerId, listingId);

        var removed = await Service().RemoveAsync(BuyerId, listingId);

        Assert.True(removed);
        Assert.Equal(0, await Service().CountAsync(BuyerId));
    }

    [Fact]
    public async Task Removing_something_that_was_never_there_is_not_an_error()
    {
        var listingId = await GivenListing(ListingStatus.Active);

        var removed = await Service().RemoveAsync(BuyerId, listingId);

        Assert.False(removed);
    }

    [Fact]
    public async Task Everyone_has_their_own_list()
    {
        var listingId = await GivenListing(ListingStatus.Active);
        await Service().AddAsync(BuyerId, listingId);

        // Продавець нічого не відкладав, тож його список порожній — навіть
        // якщо йдеться про те саме оголошення.
        Assert.Equal(0, await Service().CountAsync(SellerId));
    }

    [Fact]
    public async Task The_page_shows_the_most_recently_added_first()
    {
        var first = await GivenListing(ListingStatus.Active);
        var second = await GivenListing(ListingStatus.Active);

        await Service(Now).AddAsync(BuyerId, first);
        await Service(Now.AddMinutes(5)).AddAsync(BuyerId, second);

        var page = await Service().GetPageAsync(BuyerId, page: 1, pageSize: 10);

        Assert.Equal(new[] { second, first }, page.Items.Select(item => item.Id).ToArray());
    }

    [Fact]
    public async Task The_page_marks_every_card_as_favorite()
    {
        var listingId = await GivenListing(ListingStatus.Active);
        await Service().AddAsync(BuyerId, listingId);

        var page = await Service().GetPageAsync(BuyerId, page: 1, pageSize: 10);

        // Інакше сердечко на сторінці обраного малювалося б порожнім.
        Assert.All(page.Items, item => Assert.True(item.IsFavorite));
    }

    [Fact]
    public async Task A_guest_never_sees_anything_as_favorite()
    {
        var listingId = await GivenListing(ListingStatus.Active);
        await Service().AddAsync(BuyerId, listingId);

        var mapper = Mapper(context, viewerId: null);

        var summaries = await mapper.ToSummariesAsync(context.Listings, default);

        Assert.All(summaries, item => Assert.False(item.IsFavorite));
    }

    public void Dispose()
    {
        context.Dispose();
        database.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Сервіс із наперед заданим часом. Годинник передається параметром, бо
    /// в тесті на порядок сортування важливо, щоб дві позначки лягли в базу
    /// з різним часом — із реальним UtcNow вони могли б збігтися.
    /// </summary>
    private FavoriteService Service(DateTimeOffset? now = null)
    {
        return new FavoriteService(
            context,
            new FixedClock(now ?? Now),
            Mapper(context, BuyerId));
    }

    private static ListingMapper Mapper(AutoLotDbContext context, long? viewerId)
    {
        var language = new StubLanguage();

        return new ListingMapper(
            context,
            language,
            new StubCurrentUser(viewerId),
            new GeoCatalog(context, language));
    }

    private async Task<long> GivenListing(ListingStatus status)
    {

        await EnsureReferenceData(context);

        var listing = new Listing
        {
            Title = "Тестове авто",
            Description = "Опис",
            SellerId = SellerId,
            CityId = 1,
            Price = 10_000,
            Currency = Currency.Usd,
            PriceUah = 420_000,
            Status = status,
            Car = new Car
            {
                Year = 2020,
                MakeId = 1,
                ModelId = 1,
                FuelType = FuelType.Petrol,
                Transmission = TransmissionType.Manual,
                Drivetrain = DrivetrainType.FrontWheel,
                BodyType = BodyType.Sedan,
                Color = CarColor.Black,
            },
        };

        context.Listings.Add(listing);
        await context.SaveChangesAsync();

        return listing.Id;
    }

    private async Task ChangeStatus(long listingId, ListingStatus status)
    {

        var listing = await context.Listings.FindAsync(listingId)
            ?? throw new InvalidOperationException($"Оголошення {listingId} немає.");

        listing.Status = status;
        await context.SaveChangesAsync();
    }

    /// <summary>Мінімум довідників, без яких оголошення не збережеться.</summary>
    private static async Task EnsureReferenceData(AutoLotDbContext context)
    {
        if (await context.Cities.FindAsync(1L) is not null)
        {
            return;
        }

        context.Regions.Add(new Region { Id = 1, Code = "kyiv-region" });
        context.Cities.Add(new City { Id = 1, RegionId = 1, Code = "kyiv" });
        context.Makes.Add(new Make { Id = 1, Name = "BMW", Slug = "bmw" });
        context.Models.Add(new Model { Id = 1, MakeId = 1, Name = "X5", Slug = "x5" });

        context.Users.Add(new User
        {
            Id = BuyerId,
            UserName = "buyer@example.com",
            Email = "buyer@example.com",
            DisplayName = "Покупець",
        });

        context.Users.Add(new User
        {
            Id = SellerId,
            UserName = "seller@example.com",
            Email = "seller@example.com",
            DisplayName = "Продавець",
        });

        await context.SaveChangesAsync();
    }

}
