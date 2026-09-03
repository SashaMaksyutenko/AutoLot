using AutoLot.Application.Listings;
using AutoLot.Domain.Auctions;
using AutoLot.Domain.Cars;
using AutoLot.Domain.Chat;
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
/// Угода: хто може бути покупцем і що з цього записується.
///
/// Головне правило перевіряється тут — покупця беруть лише з тих, з ким
/// справді була справа. Воно потрібне не саме по собі: з появою відгуків
/// приписаний покупець означав би право написати відгук незнайомцю.
/// </summary>
public class DealTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 8, 31, 12, 0, 0, TimeSpan.Zero);

    private const long SellerId = 1;

    private const long WriterId = 2;

    private const long SilentId = 3;

    private readonly TestDatabase database = new();

    private readonly AutoLotDbContext context;

    private readonly long listingId;

    public DealTests()
    {
        context = database.CreateContext();
        Seed();
        listingId = NewListing();
    }

    [Fact]
    public async Task Whoever_wrote_about_the_car_may_be_the_buyer()
    {
        StartConversation(listingId, WriterId);

        var candidates = await Service().GetBuyerCandidatesAsync(listingId, SellerId);

        var candidate = Assert.Single(candidates);
        Assert.Equal(WriterId, candidate.Id);
        Assert.Equal("Писав", candidate.DisplayName);
        Assert.False(candidate.IsAuctionWinner);
    }

    [Fact]
    public async Task Nobody_wrote_means_nobody_to_choose()
    {
        // Порожній список не помилка: продати могли й поза майданчиком.
        Assert.Empty(await Service().GetBuyerCandidatesAsync(listingId, SellerId));
    }

    [Fact]
    public async Task The_freshest_conversation_comes_first()
    {
        StartConversation(listingId, SilentId, Now.AddDays(-30));
        StartConversation(listingId, WriterId, Now.AddMinutes(-5));

        var candidates = await Service().GetBuyerCandidatesAsync(listingId, SellerId);

        // З тим, хто писав учора, угода ймовірніша, ніж із тим, хто питав
        // місяць тому.
        Assert.Equal(WriterId, candidates[0].Id);
    }

    [Fact]
    public async Task A_stranger_cannot_see_who_wrote_to_the_seller()
    {
        StartConversation(listingId, WriterId);

        await Assert.ThrowsAsync<ListingAccessException>(
            () => Service().GetBuyerCandidatesAsync(listingId, SilentId));
    }

    [Fact]
    public async Task The_sale_records_the_buyer_and_the_moment()
    {
        StartConversation(listingId, WriterId);

        await Service().MarkSoldAsync(listingId, SellerId, WriterId);

        var listing = await context.Listings.AsNoTracking().SingleAsync(item => item.Id == listingId);

        Assert.Equal(ListingStatus.Sold, listing.Status);
        Assert.Equal(WriterId, listing.BuyerId);
        Assert.Equal(Now, listing.SoldAt);
    }

    [Fact]
    public async Task A_sale_outside_the_platform_keeps_no_buyer()
    {
        await Service().MarkSoldAsync(listingId, SellerId, buyerId: null);

        var listing = await context.Listings.AsNoTracking().SingleAsync(item => item.Id == listingId);

        Assert.Equal(ListingStatus.Sold, listing.Status);
        Assert.Null(listing.BuyerId);
    }

    [Fact]
    public async Task Someone_who_never_wrote_cannot_be_named_buyer()
    {
        StartConversation(listingId, WriterId);

        // Інакше продавець міг би приписати угоду будь-кому.
        var refused = await Assert.ThrowsAsync<ListingDataException>(
            () => Service().MarkSoldAsync(listingId, SellerId, SilentId));

        Assert.Contains("не листувалася", refused.Message, StringComparison.Ordinal);

        var listing = await context.Listings.AsNoTracking().SingleAsync(item => item.Id == listingId);
        Assert.Equal(ListingStatus.Active, listing.Status);
    }

    [Fact]
    public async Task A_stranger_cannot_close_the_deal()
    {
        StartConversation(listingId, WriterId);

        await Assert.ThrowsAsync<ListingAccessException>(
            () => Service().MarkSoldAsync(listingId, WriterId, WriterId));
    }

    [Fact]
    public async Task On_an_auction_lot_the_winner_is_the_only_candidate()
    {
        // Той, хто просто листувався, не має ставати покупцем в обхід торгів.
        StartConversation(listingId, SilentId);
        WinAuction(listingId, WriterId);

        var candidates = await Service().GetBuyerCandidatesAsync(listingId, SellerId);

        var winner = Assert.Single(candidates);
        Assert.Equal(WriterId, winner.Id);
        Assert.True(winner.IsAuctionWinner);
    }

    [Fact]
    public async Task On_an_auction_lot_nobody_else_may_be_named_buyer()
    {
        StartConversation(listingId, SilentId);
        WinAuction(listingId, WriterId);

        await Assert.ThrowsAsync<ListingDataException>(
            () => Service().MarkSoldAsync(listingId, SellerId, SilentId));
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
            new FixedClock(Now),
            new ListingMapper(context, language, new StubCurrentUser(SellerId), geo),
            new ListingAccess(context),
            new StubListingAllowance());
    }

    private void StartConversation(long listing, long buyerId, DateTimeOffset? lastMessageAt = null)
    {
        context.Conversations.Add(new Conversation
        {
            ListingId = listing,
            BuyerId = buyerId,
            CreatedAt = Now,
            LastMessageAt = lastMessageAt ?? Now,
        });

        context.SaveChanges();
    }

    private void WinAuction(long listing, long winnerId)
    {
        context.Auctions.Add(new Auction
        {
            ListingId = listing,
            StartPrice = 1_000m,
            CurrentPrice = 2_000m,
            EndsAt = Now.AddDays(-1),
            Status = AuctionStatus.Ended,
            WinnerId = winnerId,
        });

        context.SaveChanges();
    }

    private long NewListing()
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

        context.Users.Add(NewUser(SellerId, "seller", "Продавець"));
        context.Users.Add(NewUser(WriterId, "writer", "Писав"));
        context.Users.Add(NewUser(SilentId, "silent", "Мовчав"));

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
