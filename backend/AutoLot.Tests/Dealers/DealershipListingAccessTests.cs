using AutoLot.Application.Listings;
using AutoLot.Domain.Cars;
using AutoLot.Domain.Dealers;
using AutoLot.Domain.Enums;
using AutoLot.Domain.Geo;
using AutoLot.Domain.Identity;
using AutoLot.Domain.Listings;
using AutoLot.Infrastructure.Listings;
using AutoLot.Infrastructure.Persistence;
using AutoLot.Tests.TestDoubles;

namespace AutoLot.Tests.Dealers;

/// <summary>
/// Головна зміна цього пункту: «моє оголошення» більше не означає «я його
/// подав». Оголошенням салону керує будь-хто з персоналу — саме заради цього
/// салон і зроблено окремою сутністю.
///
/// Перевіряється сам <see cref="ListingAccess"/>: він один відповідає на це
/// питання у восьми місцях проєкту, тож помилка тут проявилася б скрізь.
/// </summary>
public class DealershipListingAccessTests : IDisposable
{
    private const long DealershipId = 1;

    private const long OtherDealershipId = 2;

    /// <summary>Менеджер, який подав оголошення від імені салону.</summary>
    private const long AuthorId = 10;

    /// <summary>Його колега з того самого салону.</summary>
    private const long ColleagueId = 11;

    /// <summary>Менеджер іншого салону.</summary>
    private const long RivalId = 12;

    private const long PrivateSellerId = 13;

    private readonly TestDatabase database = new();

    private readonly AutoLotDbContext context;

    public DealershipListingAccessTests()
    {
        context = database.CreateContext();
        Seed();
    }

    [Fact]
    public async Task The_author_manages_their_own_listing()
    {
        var listing = SalonListing();

        Assert.True(await Access().CanManageAsync(listing, AuthorId));
    }

    [Fact]
    public async Task A_colleague_manages_the_same_listing()
    {
        var listing = SalonListing();

        // Заради цього все й робилося: менеджер поїхав у відпустку, а лот
        // має кому вести.
        Assert.True(await Access().CanManageAsync(listing, ColleagueId));
    }

    [Fact]
    public async Task A_manager_from_another_salon_does_not()
    {
        var listing = SalonListing();

        Assert.False(await Access().CanManageAsync(listing, RivalId));
    }

    [Fact]
    public async Task A_stranger_does_not()
    {
        var listing = SalonListing();

        Assert.False(await Access().CanManageAsync(listing, PrivateSellerId));
    }

    [Fact]
    public async Task A_private_listing_stays_personal()
    {
        var listing = PrivateListing();

        Assert.True(await Access().CanManageAsync(listing, PrivateSellerId));

        // Салон тут ні до чого — оголошення нічиє, крім автора.
        Assert.False(await Access().CanManageAsync(listing, ColleagueId));
    }

    [Fact]
    public async Task A_dismissed_manager_loses_access_but_the_listing_stays()
    {
        var listing = SalonListing();
        context.Listings.Add(listing);
        await context.SaveChangesAsync();

        var member = context.DealershipMembers.Single(
            item => item.DealershipId == DealershipId && item.UserId == ColleagueId);

        context.DealershipMembers.Remove(member);
        await context.SaveChangesAsync();

        Assert.False(await Access().CanManageAsync(listing, ColleagueId));

        // А от саме оголошення нікуди не поділося — воно належить салону,
        // а не людині. Це і є причина, чому DealershipId додали окремо, а не
        // замінили ним SellerId.
        Assert.Single(context.Listings);
        Assert.Equal(DealershipId, context.Listings.Single().DealershipId);
    }

    [Fact]
    public async Task My_listings_include_the_whole_salon()
    {
        context.Listings.Add(SalonListing());
        context.Listings.Add(PrivateListing());
        await context.SaveChangesAsync();

        var dealershipIds = await Access().DealershipIdsOfAsync(ColleagueId);

        var mine = ListingAccess
            .ManagedBy(context.Listings, ColleagueId, dealershipIds)
            .ToList();

        // Колега не подавав жодного оголошення особисто, але салонне бачить.
        var listing = Assert.Single(mine);
        Assert.Equal(AuthorId, listing.SellerId);
    }

    [Fact]
    public async Task Someone_without_a_salon_sees_only_their_own()
    {
        context.Listings.Add(SalonListing());
        context.Listings.Add(PrivateListing());
        await context.SaveChangesAsync();

        var dealershipIds = await Access().DealershipIdsOfAsync(PrivateSellerId);

        var mine = ListingAccess
            .ManagedBy(context.Listings, PrivateSellerId, dealershipIds)
            .ToList();

        var listing = Assert.Single(mine);
        Assert.Equal(PrivateSellerId, listing.SellerId);
    }

    public void Dispose()
    {
        context.Dispose();
        database.Dispose();
        GC.SuppressFinalize(this);
    }

    private ListingAccess Access() => new(context);

    private static Listing SalonListing() => NewListing(AuthorId, DealershipId);

    private static Listing PrivateListing() => NewListing(PrivateSellerId, dealershipId: null);

    private static Listing NewListing(long sellerId, long? dealershipId) => new()
    {
        Title = "Тестове авто",
        Description = "Опис",
        SellerId = sellerId,
        DealershipId = dealershipId,
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

    private void Seed()
    {
        context.Regions.Add(new Region { Id = 1, Code = "kyiv-region" });
        context.Cities.Add(new City { Id = 1, RegionId = 1, Code = "kyiv" });
        context.Makes.Add(new Make { Id = 1, Name = "BMW", Slug = "bmw" });
        context.Models.Add(new Model { Id = 1, MakeId = 1, Name = "X5", Slug = "x5" });

        context.Users.Add(NewUser(AuthorId, "author"));
        context.Users.Add(NewUser(ColleagueId, "colleague"));
        context.Users.Add(NewUser(RivalId, "rival"));
        context.Users.Add(NewUser(PrivateSellerId, "private"));

        context.Dealerships.Add(NewDealership(DealershipId, "Авто Плюс", "avto-plyus"));
        context.Dealerships.Add(NewDealership(OtherDealershipId, "Мотор Сіті", "motor-siti"));

        context.SaveChanges();

        context.DealershipMembers.Add(NewMember(DealershipId, AuthorId, DealershipRole.Owner));
        context.DealershipMembers.Add(NewMember(DealershipId, ColleagueId, DealershipRole.Manager));
        context.DealershipMembers.Add(NewMember(OtherDealershipId, RivalId, DealershipRole.Owner));

        context.SaveChanges();
    }

    private static Dealership NewDealership(long id, string name, string slug) => new()
    {
        Id = id,
        Name = name,
        Slug = slug,
        CityId = 1,
    };

    private static DealershipMember NewMember(long dealershipId, long userId, DealershipRole role) => new()
    {
        DealershipId = dealershipId,
        UserId = userId,
        Role = role,
    };

    private static User NewUser(long id, string login) => new()
    {
        Id = id,
        UserName = $"{login}@example.com",
        Email = $"{login}@example.com",
        NormalizedEmail = $"{login}@example.com".ToUpperInvariant(),
        DisplayName = login,
    };
}
