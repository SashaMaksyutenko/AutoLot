using AutoLot.Domain.Cars;
using AutoLot.Domain.Enums;
using AutoLot.Domain.Geo;
using AutoLot.Domain.Identity;
using AutoLot.Domain.Listings;
using AutoLot.Infrastructure.Geo;
using AutoLot.Infrastructure.Listings;
using AutoLot.Infrastructure.Persistence;
using AutoLot.Tests.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AutoLot.Tests.Listings;

/// <summary>
/// Історія переглядів.
///
/// Головне тут — що це перелік АВТО, а не журнал кліків: десять заходів на
/// те саме оголошення дають один пункт, а не десять. І що в історію не
/// потрапляє те, чого людина не переглядала як покупець: власні оголошення
/// й чужі чернетки.
/// </summary>
public class ViewHistoryTests : IDisposable
{
    private const long ViewerId = 1;
    private const long SellerId = 2;

    private static readonly DateTimeOffset Now = new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);

    private readonly TestDatabase database = new();

    private readonly AutoLotDbContext context;

    public ViewHistoryTests()
    {
        context = database.CreateContext();
        Seed();
    }

    [Fact]
    public async Task Opening_a_listing_puts_it_into_the_history()
    {
        var listingId = NewListing();

        await Service().GetAsync(listingId, ViewerId, false);

        var history = await Service().GetRecentlyViewedAsync(ViewerId, 10);

        Assert.Equal(listingId, Assert.Single(history).Id);
    }

    [Fact]
    public async Task Opening_the_same_listing_twice_gives_one_entry()
    {
        var listingId = NewListing();

        await Service().GetAsync(listingId, ViewerId, false);
        await ServiceAt(Now.AddMinutes(5)).GetAsync(listingId, ViewerId, false);

        Assert.Single(await Service().GetRecentlyViewedAsync(ViewerId, 10));
        Assert.Equal(1, await context.ListingViews.CountAsync());
    }

    [Fact]
    public async Task The_freshest_comes_first()
    {
        var first = NewListing();
        var second = NewListing();

        await Service().GetAsync(first, ViewerId, false);
        await ServiceAt(Now.AddMinutes(1)).GetAsync(second, ViewerId, false);

        // Повертаємося до першого — і воно має піднятися нагору.
        await ServiceAt(Now.AddMinutes(2)).GetAsync(first, ViewerId, false);

        var history = await Service().GetRecentlyViewedAsync(ViewerId, 10);

        Assert.Equal([first, second], history.Select(item => item.Id));
    }

    [Fact]
    public async Task A_guest_leaves_no_history()
    {
        var listingId = NewListing();

        await Service().GetAsync(listingId, null, false);

        // Гостя ми не впізнаємо між заходами, тож і писати нема кому.
        Assert.Equal(0, await context.ListingViews.CountAsync());
    }

    [Fact]
    public async Task Your_own_listing_does_not_land_in_your_history()
    {
        var listingId = NewListing();

        await Service().GetAsync(listingId, SellerId, false);

        // Історія відповідає на питання «що я приглядав», а власне авто
        // продавець відкриває з іншої причини.
        Assert.Equal(0, await context.ListingViews.CountAsync());
    }

    [Fact]
    public async Task A_listing_taken_off_sale_drops_out_of_the_history()
    {
        var listingId = NewListing();
        await Service().GetAsync(listingId, ViewerId, false);

        var listing = await context.Listings.SingleAsync(item => item.Id == listingId);
        listing.Status = ListingStatus.Archived;
        await context.SaveChangesAsync();

        // Рядок, який нікуди не веде, гірший за коротшу історію.
        Assert.Empty(await Service().GetRecentlyViewedAsync(ViewerId, 10));
    }

    [Fact]
    public async Task Someone_elses_history_is_not_mine()
    {
        var listingId = NewListing();

        await Service().GetAsync(listingId, ViewerId, false);

        Assert.Empty(await Service().GetRecentlyViewedAsync(SellerId, 10));
    }

    [Fact]
    public async Task The_history_does_not_grow_past_its_limit()
    {
        // На один більше за межу: найдавніший пункт має зникнути.
        var ids = new List<long>();

        for (var index = 0; index <= ListingView.PerUserLimit; index++)
        {
            var listingId = NewListing();
            ids.Add(listingId);

            await ServiceAt(Now.AddMinutes(index)).GetAsync(listingId, ViewerId, false);
        }

        Assert.Equal(ListingView.PerUserLimit, await context.ListingViews.CountAsync());

        // Зник саме перший, а не випадковий.
        Assert.False(await context.ListingViews.AnyAsync(view => view.ListingId == ids[0]));
    }

    public void Dispose()
    {
        context.Dispose();
        database.Dispose();
        GC.SuppressFinalize(this);
    }

    private ListingService Service() => ServiceAt(Now);

    private ListingService ServiceAt(DateTimeOffset now)
    {
        var language = new StubLanguage();
        var geo = new GeoCatalog(context, language);

        return new ListingService(
            context,
            geo,
            new StubExchangeRates(),
            new FixedClock(now),
            new ListingMapper(context, language, new StubCurrentUser(ViewerId), geo),
            new ListingAccess(context),
            new StubListingAllowance(),
            NullLogger<ListingService>.Instance);
    }

    private long NewListing()
    {
        var listing = new Listing
        {
            Title = "Тестове авто",
            Description = "Опис",
            SellerId = SellerId,
            CityId = 1,
            Price = 10_000m,
            Currency = Currency.Usd,
            PriceUah = 420_000m,
            Status = ListingStatus.Active,
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

        context.Users.AddRange(
            new User
            {
                Id = ViewerId,
                UserName = "viewer@example.com",
                Email = "viewer@example.com",
                DisplayName = "Покупець",
            },
            new User
            {
                Id = SellerId,
                UserName = "seller@example.com",
                Email = "seller@example.com",
                DisplayName = "Продавець",
            });

        context.SaveChanges();
    }
}
