using AutoLot.Domain.Cars;
using AutoLot.Domain.Enums;
using AutoLot.Domain.Geo;
using AutoLot.Domain.Identity;
using AutoLot.Domain.Listings;
using AutoLot.Infrastructure.Geo;
using AutoLot.Infrastructure.Listings;
using AutoLot.Infrastructure.Persistence;
using AutoLot.Tests.TestDoubles;

namespace AutoLot.Tests.Listings;

/// <summary>
/// Вибірка кількох оголошень для порівняння.
///
/// Головне тут — що в таблицю не проступає нічого зайвого: ні чужа чернетка,
/// ні знята з публікації машина. І що порядок колонок той, у якому людина
/// додавала авто, а не той, у якому їх повернула база.
/// </summary>
public class CompareTests : IDisposable
{
    private const long SellerId = 1;

    private readonly TestDatabase database = new();

    private readonly AutoLotDbContext context;

    public CompareTests()
    {
        context = database.CreateContext();
        Seed();
    }

    [Fact]
    public async Task Nothing_asked_nothing_returned()
    {
        Assert.Empty(await Service().GetManyAsync([]));
    }

    [Fact]
    public async Task Several_listings_come_back_together()
    {
        var first = NewListing();
        var second = NewListing();

        var found = await Service().GetManyAsync([first, second]);

        Assert.Equal(2, found.Count);
    }

    [Fact]
    public async Task The_order_is_the_one_that_was_asked_for()
    {
        var first = NewListing();
        var second = NewListing();
        var third = NewListing();

        var found = await Service().GetManyAsync([third, first, second]);

        // Людина додавала авто в певній послідовності, і колонки мають
        // стояти так само — інакше таблиця щоразу перебудовувалася б.
        Assert.Equal([third, first, second], found.Select(item => item.Id));
    }

    [Fact]
    public async Task Only_published_listings_appear()
    {
        var active = NewListing();
        var draft = NewListing(ListingStatus.Draft);
        var archived = NewListing(ListingStatus.Archived);
        var rejected = NewListing(ListingStatus.Rejected);

        var found = await Service().GetManyAsync([active, draft, archived, rejected]);

        // Чужа чернетка не має проступати навіть колонкою в таблиці.
        Assert.Equal(active, Assert.Single(found).Id);
    }

    [Fact]
    public async Task A_missing_listing_is_simply_absent()
    {
        var real = NewListing();

        var found = await Service().GetManyAsync([real, 999_999]);

        // У порівнянні немає кому показувати «такого немає»: або є колонка,
        // або її немає.
        Assert.Equal(real, Assert.Single(found).Id);
    }

    [Fact]
    public async Task The_same_listing_asked_twice_comes_once()
    {
        var listingId = NewListing();

        var found = await Service().GetManyAsync([listingId, listingId]);

        Assert.Single(found);
    }

    [Fact]
    public async Task The_phone_number_stays_hidden()
    {
        var listingId = NewListing();

        var found = await Service().GetManyAsync([listingId]);

        // Порівняння відкрите без входу, тож приватні поля сюди не потрапляють
        // так само, як і в публічну картку авто.
        Assert.Null(found[0].Seller.PhoneNumber);
    }

    public void Dispose()
    {
        context.Dispose();
        database.Dispose();
        GC.SuppressFinalize(this);
    }

    private ListingService Service()
    {
        var language = new StubLanguage();
        var geo = new GeoCatalog(context, language);

        return new ListingService(
            context,
            geo,
            new StubExchangeRates(),
            new FixedClock(new DateTimeOffset(2026, 9, 5, 12, 0, 0, TimeSpan.Zero)),
            new ListingMapper(context, language, new StubCurrentUser(), geo),
            new ListingAccess(context),
            new StubListingAllowance());
    }

    private long NewListing(ListingStatus status = ListingStatus.Active)
    {
        var listing = new Listing
        {
            Title = "Тестовий лот",
            Description = "Опис",
            SellerId = SellerId,
            CityId = 1,
            Price = 10_000m,
            Currency = Currency.Usd,
            PriceUah = 420_000m,
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
        context.SaveChanges();

        return listing.Id;
    }

    private void Seed()
    {
        context.Regions.Add(new Region { Id = 1, Code = "kyiv-region" });
        context.Cities.Add(new City { Id = 1, RegionId = 1, Code = "kyiv" });
        context.Makes.Add(new Make { Id = 1, Name = "BMW", Slug = "bmw" });
        context.Models.Add(new Model { Id = 1, MakeId = 1, Name = "X5", Slug = "x5" });

        context.Users.Add(new User
        {
            Id = SellerId,
            UserName = "seller@example.com",
            Email = "seller@example.com",
            PhoneNumber = "+380671234567",
            DisplayName = "Продавець",
        });

        context.SaveChanges();
    }
}
