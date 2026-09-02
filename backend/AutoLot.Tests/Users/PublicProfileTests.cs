using AutoLot.Application.Listings.Dtos;
using AutoLot.Domain.Cars;
using AutoLot.Domain.Dealers;
using AutoLot.Domain.Enums;
using AutoLot.Domain.Geo;
using AutoLot.Domain.Identity;
using AutoLot.Domain.Listings;
using AutoLot.Application.Users.Dtos;
using AutoLot.Infrastructure.Geo;
using AutoLot.Infrastructure.Identity;
using AutoLot.Infrastructure.Listings;
using AutoLot.Infrastructure.Persistence;
using AutoLot.Tests.TestDoubles;
using Microsoft.EntityFrameworkCore;

namespace AutoLot.Tests.Users;

/// <summary>
/// Публічний профіль продавця й список покупок.
///
/// Профіль перевіряється передусім на те, чого в ньому НЕ має бути: він
/// віддається без токена, тож будь-яке зайве поле стало б відкритим для
/// збирачів даних.
/// </summary>
public class PublicProfileTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 9, 2, 12, 0, 0, TimeSpan.Zero);

    private const long SellerId = 1;

    private const long BuyerId = 2;

    private const long DealershipId = 1;

    private readonly TestDatabase database = new();

    private readonly AutoLotDbContext context;

    public PublicProfileTests()
    {
        context = database.CreateContext();
        Seed();
    }

    [Fact]
    public async Task The_public_profile_carries_no_private_field()
    {
        var profile = await Profiles().GetAsync(SellerId);

        Assert.NotNull(profile);
        Assert.Equal("Продавець", profile.DisplayName);
        Assert.Equal(Now.AddYears(-2), profile.JoinedAt);
        Assert.Equal(AccountType.Private, profile.AccountType);

        // Тип узагалі не має полів пошти, телефону й ролей — саме тому він
        // окремий від UserProfile, а не той самий із порожніми значеннями.
        Assert.DoesNotContain(
            typeof(PublicProfile).GetProperties(),
            property => property.Name is "Email" or "PhoneNumber" or "Roles");
    }

    [Fact]
    public async Task An_unknown_person_has_no_profile()
    {
        Assert.Null(await Profiles().GetAsync(999_999));
    }

    [Fact]
    public async Task The_profile_counts_only_published_listings()
    {
        NewListing(ListingStatus.Active);
        NewListing(ListingStatus.Active);
        NewListing(ListingStatus.Draft);
        NewListing(ListingStatus.Archived);

        var profile = await Profiles().GetAsync(SellerId);

        // Чернетки й архів — справа господаря, стороннім їх не рахують.
        Assert.Equal(2, profile!.ActiveListingCount);
    }

    [Fact]
    public async Task A_salon_worker_shows_the_salon()
    {
        context.DealershipMembers.Add(new DealershipMember
        {
            DealershipId = DealershipId,
            UserId = SellerId,
            Role = DealershipRole.Manager,
        });

        await context.SaveChangesAsync();

        var profile = await Profiles().GetAsync(SellerId);

        Assert.Equal("Авто Плюс", profile!.Dealer?.Name);
        Assert.True(profile.Dealer?.IsVerified);
    }

    [Fact]
    public async Task A_private_seller_shows_no_salon()
    {
        Assert.Null((await Profiles().GetAsync(SellerId))!.Dealer);
    }

    [Fact]
    public async Task The_profile_carries_the_rating()
    {
        var listingId = NewListing(ListingStatus.Active);
        SellTo(listingId, BuyerId);
        await Reviews().LeaveAsync(listingId, BuyerId, new LeaveReviewRequest { Rating = 4 });

        var profile = await Profiles().GetAsync(SellerId);

        Assert.Equal(1, profile!.Rating.Count);
        Assert.Equal(4m, profile.Rating.Average);
    }

    [Fact]
    public async Task Without_reviews_the_rating_is_empty_rather_than_zero()
    {
        var profile = await Profiles().GetAsync(SellerId);

        Assert.Equal(0, profile!.Rating.Count);
    }

    [Fact]
    public async Task Purchases_are_what_the_person_bought()
    {
        var mine = NewListing(ListingStatus.Active);
        SellTo(mine, BuyerId);

        var someoneElses = NewListing(ListingStatus.Active);
        SellTo(someoneElses, buyerId: null);

        var purchased = await Listings().GetPurchasedAsync(BuyerId);

        var single = Assert.Single(purchased);
        Assert.Equal(mine, single.Id);
    }

    [Fact]
    public async Task A_sale_outside_the_platform_belongs_to_nobody()
    {
        var listingId = NewListing(ListingStatus.Active);
        SellTo(listingId, buyerId: null);

        // Покупця не записано — покупки немає в жодного списку.
        Assert.Empty(await Listings().GetPurchasedAsync(BuyerId));
    }

    [Fact]
    public async Task The_newest_purchase_comes_first()
    {
        var older = NewListing(ListingStatus.Active);
        SellTo(older, BuyerId, Now.AddDays(-10));

        var newer = NewListing(ListingStatus.Active);
        SellTo(newer, BuyerId, Now);

        var purchased = await Listings().GetPurchasedAsync(BuyerId);

        Assert.Equal(newer, purchased[0].Id);
    }

    public void Dispose()
    {
        context.Dispose();
        database.Dispose();
        GC.SuppressFinalize(this);
    }

    private PublicProfileService Profiles()
    {
        var language = new StubLanguage();

        return new PublicProfileService(context, new GeoCatalog(context, language));
    }

    private ReviewService Reviews() =>
        new(context, new FixedClock(Now), new ListingAccess(context));

    private ListingService Listings()
    {
        var language = new StubLanguage();
        var geo = new GeoCatalog(context, language);

        return new ListingService(
            context,
            geo,
            new StubExchangeRates(),
            new FixedClock(Now),
            new ListingMapper(context, language, new StubCurrentUser(BuyerId), geo),
            new ListingAccess(context));
    }

    private void SellTo(long listingId, long? buyerId, DateTimeOffset? soldAt = null)
    {
        var listing = context.Listings.Single(item => item.Id == listingId);

        listing.MarkSold(soldAt ?? Now, buyerId);
        context.SaveChanges();
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
            CreatedAt = Now.AddYears(-2),
        });

        context.Users.Add(new User
        {
            Id = BuyerId,
            UserName = "buyer@example.com",
            Email = "buyer@example.com",
            DisplayName = "Покупець",
            CreatedAt = Now,
        });

        context.Dealerships.Add(new Dealership
        {
            Id = DealershipId,
            Name = "Авто Плюс",
            Slug = "avto-plyus",
            CityId = 1,
            IsVerified = true,
        });

        context.SaveChanges();
    }
}
