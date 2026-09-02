using AutoLot.Application.Listings;
using AutoLot.Application.Listings.Dtos;
using AutoLot.Domain.Cars;
using AutoLot.Domain.Common;
using AutoLot.Domain.Dealers;
using AutoLot.Domain.Enums;
using AutoLot.Domain.Geo;
using AutoLot.Domain.Identity;
using AutoLot.Domain.Listings;
using AutoLot.Infrastructure.Listings;
using AutoLot.Infrastructure.Persistence;
using AutoLot.Tests.TestDoubles;
using Microsoft.EntityFrameworkCore;

namespace AutoLot.Tests.Listings;

/// <summary>
/// Взаємні відгуки. Головне тут — що право писати дає угода, а не бажання:
/// сторонній не пише нікому, і навіть сторони не пишуть, доки продажу не
/// сталося.
/// </summary>
public class ReviewServiceTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    private const long SellerId = 1;

    private const long BuyerId = 2;

    private const long StrangerId = 3;

    private const long ColleagueId = 4;

    private const long DealershipId = 1;

    private readonly TestDatabase database = new();

    private readonly AutoLotDbContext context;

    private readonly long listingId;

    public ReviewServiceTests()
    {
        context = database.CreateContext();
        Seed();
        listingId = NewListing(dealershipId: null);
    }

    [Fact]
    public async Task Nobody_may_review_a_listing_that_was_not_sold()
    {
        var state = await Service().GetForListingAsync(listingId, BuyerId);

        Assert.False(state.CanReview);

        await Assert.ThrowsAsync<ReviewNotAllowedException>(
            () => Service().LeaveAsync(listingId, BuyerId, Review(5)));
    }

    [Fact]
    public async Task A_sale_without_a_recorded_buyer_has_no_second_side()
    {
        Sell(listingId, buyerId: null);

        // Продано поза майданчиком — другої сторони просто не існує.
        Assert.False((await Service().GetForListingAsync(listingId, BuyerId)).CanReview);
        Assert.False((await Service().GetForListingAsync(listingId, SellerId)).CanReview);
    }

    [Fact]
    public async Task The_buyer_reviews_the_seller()
    {
        Sell(listingId, BuyerId);

        var review = await Service().LeaveAsync(listingId, BuyerId, Review(5, "Усе чесно"));

        // Про кого відгук, у запиті не вказують — це виводиться зі складу
        // угоди, тож приписати його сторонньому неможливо.
        Assert.Equal(SellerId, review.SubjectId);
        Assert.Equal(BuyerId, review.AuthorId);
        Assert.False(review.AuthorIsSeller);
        Assert.Equal(5, review.Rating);
        Assert.Equal("Усе чесно", review.Text);
    }

    [Fact]
    public async Task The_seller_reviews_the_buyer()
    {
        Sell(listingId, BuyerId);

        var review = await Service().LeaveAsync(listingId, SellerId, Review(4));

        Assert.Equal(BuyerId, review.SubjectId);
        Assert.True(review.AuthorIsSeller);
        Assert.Null(review.Text);
    }

    [Fact]
    public async Task A_stranger_may_read_but_not_write()
    {
        Sell(listingId, BuyerId);
        await Service().LeaveAsync(listingId, BuyerId, Review(5, "Рекомендую"));

        var state = await Service().GetForListingAsync(listingId, StrangerId);

        // Відгуки публічні — у цьому вся їхня користь.
        Assert.False(state.CanReview);
        Assert.Null(state.MineId);
        Assert.Equal("Рекомендую", Assert.Single(state.Reviews).Text);

        await Assert.ThrowsAsync<ReviewNotAllowedException>(
            () => Service().LeaveAsync(listingId, StrangerId, Review(1)));
    }

    [Fact]
    public async Task A_guest_sees_every_review_and_none_as_their_own()
    {
        Sell(listingId, BuyerId);
        await Service().LeaveAsync(listingId, BuyerId, Review(5, "Рекомендую"));

        var state = await Service().GetForListingAsync(listingId, viewerId: null);

        // Гість і є той, заради кого відгуки публічні: він дивиться
        // репутацію ДО того, як написати продавцю. Показати йому порожньо
        // означало б знецінити весь механізм.
        Assert.Equal("Рекомендую", Assert.Single(state.Reviews).Text);
        Assert.False(state.CanReview);
        Assert.Null(state.MineId);
    }

    [Fact]
    public async Task A_review_is_written_once()
    {
        Sell(listingId, BuyerId);
        await Service().LeaveAsync(listingId, BuyerId, Review(5));

        // Відгук, який можна переписати після сварки, перестає бути
        // свідченням про угоду.
        await Assert.ThrowsAsync<ReviewNotAllowedException>(
            () => Service().LeaveAsync(listingId, BuyerId, Review(1)));

        Assert.False((await Service().GetForListingAsync(listingId, BuyerId)).CanReview);
    }

    [Fact]
    public async Task Each_side_sees_its_own_and_the_others()
    {
        Sell(listingId, BuyerId);
        await Service().LeaveAsync(listingId, BuyerId, Review(5, "Від покупця"));
        await Service().LeaveAsync(listingId, SellerId, Review(4, "Від продавця"));

        var asBuyer = await Service().GetForListingAsync(listingId, BuyerId);

        Assert.Equal(2, asBuyer.Reviews.Count);
        Assert.Equal(
            "Від покупця",
            asBuyer.Reviews.Single(review => review.Id == asBuyer.MineId).Text);
        Assert.Equal(
            "Від продавця",
            asBuyer.Reviews.Single(review => review.Id != asBuyer.MineId).Text);
        Assert.False(asBuyer.CanReview);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public async Task The_rating_stays_within_the_scale(int rating)
    {
        Sell(listingId, BuyerId);

        await Assert.ThrowsAsync<DomainRuleException>(
            () => Service().LeaveAsync(listingId, BuyerId, Review(rating)));
    }

    [Fact]
    public async Task A_colleague_may_review_on_behalf_of_the_salon()
    {
        var salonListingId = NewListing(DealershipId);
        Sell(salonListingId, BuyerId);

        // Правило власності те саме, що й для решти дій з оголошенням:
        // менеджер відповідає за салонний лот нарівні з колегою.
        var review = await Service().LeaveAsync(salonListingId, ColleagueId, Review(5));

        Assert.Equal(BuyerId, review.SubjectId);
        Assert.True(review.AuthorIsSeller);
    }

    [Fact]
    public async Task No_reviews_means_no_stars_rather_than_zero()
    {
        var rating = await Service().GetRatingAsync(SellerId);

        Assert.Equal(0, rating.Count);
        Assert.Equal(0m, rating.Average);
    }

    [Fact]
    public async Task The_rating_averages_every_review_about_a_person()
    {
        Sell(listingId, BuyerId);
        await Service().LeaveAsync(listingId, BuyerId, Review(5));

        var second = NewListing(dealershipId: null);
        Sell(second, StrangerId);
        await Service().LeaveAsync(second, StrangerId, Review(4));

        var rating = await Service().GetRatingAsync(SellerId);

        Assert.Equal(2, rating.Count);
        Assert.Equal(4.5m, rating.Average);
    }

    [Fact]
    public async Task The_average_is_rounded_to_one_decimal()
    {
        foreach (var score in new[] { 5, 4, 4 })
        {
            var lot = NewListing(dealershipId: null);
            Sell(lot, BuyerId);
            await Service().LeaveAsync(lot, BuyerId, Review(score));
        }

        // 13 / 3 = 4,333… — показуємо 4,3, а не всі знаки після коми.
        Assert.Equal(4.3m, (await Service().GetRatingAsync(SellerId)).Average);
    }

    [Fact]
    public async Task Reviews_about_a_person_come_newest_first()
    {
        Sell(listingId, BuyerId);
        await ServiceAt(Now).LeaveAsync(listingId, BuyerId, Review(3, "Давніший"));

        var second = NewListing(dealershipId: null);
        Sell(second, StrangerId);
        await ServiceAt(Now.AddDays(2)).LeaveAsync(second, StrangerId, Review(5, "Свіжіший"));

        var about = await Service().GetAboutAsync(SellerId);

        Assert.Equal("Свіжіший", about[0].Text);
        Assert.Equal("Давніший", about[1].Text);
    }

    public void Dispose()
    {
        context.Dispose();
        database.Dispose();
        GC.SuppressFinalize(this);
    }

    private static LeaveReviewRequest Review(int rating, string? text = null) =>
        new() { Rating = rating, Text = text };

    private ReviewService Service() => ServiceAt(Now);

    private ReviewService ServiceAt(DateTimeOffset now) =>
        new(context, new FixedClock(now), new ListingAccess(context));

    private void Sell(long listing, long? buyerId)
    {
        var entity = context.Listings.Single(item => item.Id == listing);

        entity.MarkSold(Now, buyerId);
        context.SaveChanges();
    }

    private long NewListing(long? dealershipId)
    {
        var listing = new Listing
        {
            Title = "Тестовий лот",
            Description = "Опис",
            SellerId = SellerId,
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
        context.Users.Add(NewUser(BuyerId, "buyer", "Покупець"));
        context.Users.Add(NewUser(StrangerId, "stranger", "Перехожий"));
        context.Users.Add(NewUser(ColleagueId, "colleague", "Колега"));

        context.Dealerships.Add(new Dealership
        {
            Id = DealershipId,
            Name = "Авто Плюс",
            Slug = "avto-plyus",
            CityId = 1,
        });

        context.SaveChanges();

        context.DealershipMembers.Add(new DealershipMember
        {
            DealershipId = DealershipId,
            UserId = ColleagueId,
            Role = DealershipRole.Manager,
        });

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
