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
/// Оголошення для форми редагування.
///
/// Головне тут два питання. Перше — чи справді повертаються ІДЕНТИФІКАТОРИ:
/// заради них цей метод і з'явився, бо публічна картка віддає назви, з яких
/// форму не заповнити. Друге — чи не бачить чернетку хтось чужий: вона ще
/// нікому не показувалася, і показувати її стороннім не можна навіть у
/// вигляді «немає прав» — це вже підтвердження, що вона існує.
/// </summary>
public class ListingDraftTests : IDisposable
{
    private const long SellerId = 1;
    private const long StrangerId = 2;

    private readonly TestDatabase database = new();

    private readonly AutoLotDbContext context;

    public ListingDraftTests()
    {
        context = database.CreateContext();
        Seed();
    }

    [Fact]
    public async Task The_author_gets_reference_identifiers_rather_than_names()
    {
        var listingId = NewListing();

        var draft = await Service().GetForEditAsync(listingId, SellerId);

        Assert.NotNull(draft);
        Assert.Equal(1, draft.Car.MakeId);
        Assert.Equal(1, draft.Car.ModelId);
        Assert.Equal(1, draft.CityId);
    }

    [Fact]
    public async Task The_region_comes_along_because_the_city_choice_has_two_steps()
    {
        var listingId = NewListing();

        var draft = await Service().GetForEditAsync(listingId, SellerId);

        // Самого міста формі мало: спершу вона показує область, і без неї
        // список міст не побудувати.
        Assert.Equal(1, draft!.RegionId);
    }

    [Fact]
    public async Task Chosen_features_come_back_as_identifiers()
    {
        var listingId = NewListing();
        AddFeature(listingId, featureId: 7);

        var draft = await Service().GetForEditAsync(listingId, SellerId);

        // Форма надішле цей самий набір назад, тож він має бути числами,
        // а не назвами опцій.
        Assert.Equal([7L], draft!.Car.FeatureIds);
    }

    [Fact]
    public async Task A_strangers_draft_simply_does_not_exist()
    {
        var listingId = NewListing();

        var draft = await Service().GetForEditAsync(listingId, StrangerId);

        Assert.Null(draft);
    }

    [Fact]
    public async Task A_published_listing_of_a_stranger_is_hidden_too()
    {
        // Навіть опубліковане авто чужому в ЦЬОМУ вигляді не віддається:
        // подивитися його можна карткою, а форма редагування — не для нього.
        var listingId = NewListing(ListingStatus.Active);

        Assert.Null(await Service().GetForEditAsync(listingId, StrangerId));
    }

    [Fact]
    public async Task A_missing_listing_gives_nothing()
    {
        Assert.Null(await Service().GetForEditAsync(999_999, SellerId));
    }

    [Fact]
    public async Task The_rejection_reason_travels_with_the_draft()
    {
        var listingId = NewListing(ListingStatus.Rejected, reason: "Фото не відповідають опису.");

        var draft = await Service().GetForEditAsync(listingId, SellerId);

        // Відхилене оголошення відкривають саме заради зауваження — без нього
        // людина не знає, що виправляти.
        Assert.Equal("Фото не відповідають опису.", draft!.RejectionReason);
        Assert.Equal(ListingStatus.Rejected, draft.Status);
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
            new FixedClock(new DateTimeOffset(2026, 9, 8, 12, 0, 0, TimeSpan.Zero)),
            new ListingMapper(context, language, new StubCurrentUser(SellerId), geo),
            new ListingAccess(context),
            new StubListingAllowance(),
            NullLogger<ListingService>.Instance);
    }

    private long NewListing(ListingStatus status = ListingStatus.Draft, string? reason = null)
    {
        var listing = new Listing
        {
            Title = "Тестове оголошення",
            Description = "Опис",
            SellerId = SellerId,
            CityId = 1,
            Price = 10_000m,
            Currency = Currency.Usd,
            PriceUah = 420_000m,
            Status = status,
            RejectionReason = reason,
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

    private void AddFeature(long listingId, long featureId)
    {
        var listing = context.Listings.Include(item => item.Car).Single(item => item.Id == listingId);

        context.Features.Add(new Feature
        {
            Id = featureId,
            Code = "heated-seats",
            Category = FeatureCategory.Comfort,
        });

        listing.Car.Features.Add(new CarFeature { CarId = listing.Car.Id, FeatureId = featureId });
        context.SaveChanges();
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
                Id = SellerId,
                UserName = "seller@example.com",
                Email = "seller@example.com",
                DisplayName = "Продавець",
            },
            new User
            {
                Id = StrangerId,
                UserName = "stranger@example.com",
                Email = "stranger@example.com",
                DisplayName = "Сторонній",
            });

        context.SaveChanges();
    }
}
