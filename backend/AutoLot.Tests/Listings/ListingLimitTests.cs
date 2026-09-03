using AutoLot.Domain.Cars;
using AutoLot.Domain.Common;
using AutoLot.Domain.Enums;
using AutoLot.Domain.Geo;
using AutoLot.Domain.Identity;
using AutoLot.Domain.Listings;
using AutoLot.Infrastructure.Geo;
using AutoLot.Infrastructure.Listings;
using AutoLot.Infrastructure.Persistence;
using AutoLot.Tests.TestDoubles;
using Microsoft.EntityFrameworkCore;

namespace AutoLot.Tests.Listings;

/// <summary>
/// Ліміт активних оголошень.
///
/// Головне тут — що сервіс оголошень **питає** про ліміт, а не знає його
/// сам. Раніше «п'ять» було вписане константою в код, тож зміна тарифу
/// вимагала б збірки; тепер число приходить від тарифного плану, і ці тести
/// перевіряють саме передачу, підставляючи будь-яке значення.
/// </summary>
public class ListingLimitTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 9, 2, 12, 0, 0, TimeSpan.Zero);

    private const long SellerId = 1;

    private readonly TestDatabase database = new();

    private readonly AutoLotDbContext context;

    public ListingLimitTests()
    {
        context = database.CreateContext();
        Seed();
    }

    [Fact]
    public async Task Below_the_limit_a_listing_goes_to_moderation()
    {
        var draftId = NewListing(ListingStatus.Draft);

        await Service(limit: 2).SubmitForModerationAsync(draftId, SellerId);

        var listing = await context.Listings.AsNoTracking().SingleAsync(item => item.Id == draftId);
        Assert.Equal(ListingStatus.PendingModeration, listing.Status);
    }

    [Fact]
    public async Task At_the_limit_the_next_one_is_refused()
    {
        NewListing(ListingStatus.Active);
        var draftId = NewListing(ListingStatus.Draft);

        var refused = await Assert.ThrowsAsync<DomainRuleException>(
            () => Service(limit: 1).SubmitForModerationAsync(draftId, SellerId));

        // Повідомлення називає саме ліміт тарифу, а не сталу з коду.
        Assert.Contains("1 активних", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_pending_listing_already_occupies_a_slot()
    {
        NewListing(ListingStatus.PendingModeration);
        var draftId = NewListing(ListingStatus.Draft);

        // Інакше можна було б подати десяток одразу й обійти ліміт, доки
        // модератор не дійшов до черги.
        await Assert.ThrowsAsync<DomainRuleException>(
            () => Service(limit: 1).SubmitForModerationAsync(draftId, SellerId));
    }

    [Fact]
    public async Task Drafts_and_archives_do_not_count()
    {
        NewListing(ListingStatus.Draft);
        NewListing(ListingStatus.Archived);
        NewListing(ListingStatus.Rejected);
        var draftId = NewListing(ListingStatus.Draft);

        // Місце в ліміті займає лише те, що йде у видачу.
        await Service(limit: 1).SubmitForModerationAsync(draftId, SellerId);

        var listing = await context.Listings.AsNoTracking().SingleAsync(item => item.Id == draftId);
        Assert.Equal(ListingStatus.PendingModeration, listing.Status);
    }

    [Fact]
    public async Task No_limit_means_no_limit()
    {
        for (var i = 0; i < 30; i++)
        {
            NewListing(ListingStatus.Active);
        }

        var draftId = NewListing(ListingStatus.Draft);

        // null від тарифу — «без межі», і саме так відповідає дилерський
        // акаунт та найдорожчий план.
        await Service(limit: null).SubmitForModerationAsync(draftId, SellerId);

        var listing = await context.Listings.AsNoTracking().SingleAsync(item => item.Id == draftId);
        Assert.Equal(ListingStatus.PendingModeration, listing.Status);
    }

    [Fact]
    public async Task Raising_the_plan_raises_the_limit_immediately()
    {
        NewListing(ListingStatus.Active);
        var draftId = NewListing(ListingStatus.Draft);

        await Assert.ThrowsAsync<DomainRuleException>(
            () => Service(limit: 1).SubmitForModerationAsync(draftId, SellerId));

        // Той самий лот, той самий продавець — змінився лише тариф.
        await Service(limit: 5).SubmitForModerationAsync(draftId, SellerId);

        var listing = await context.Listings.AsNoTracking().SingleAsync(item => item.Id == draftId);
        Assert.Equal(ListingStatus.PendingModeration, listing.Status);
    }

    public void Dispose()
    {
        context.Dispose();
        database.Dispose();
        GC.SuppressFinalize(this);
    }

    private ListingService Service(int? limit)
    {
        var language = new StubLanguage();
        var geo = new GeoCatalog(context, language);

        return new ListingService(
            context,
            geo,
            new StubExchangeRates(),
            new FixedClock(Now),
            new ListingMapper(context, language, new StubCurrentUser(SellerId), geo),
            new ListingAccess(context),
            new StubListingAllowance(limit));
    }

    private long NewListing(ListingStatus status)
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
            DisplayName = "Продавець",
            AccountType = AccountType.Private,
        });

        context.SaveChanges();
    }
}
