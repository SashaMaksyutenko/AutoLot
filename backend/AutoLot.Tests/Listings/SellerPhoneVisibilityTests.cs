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
/// Телефон продавця на сторінці авто.
///
/// Головне правило: гостю номер не віддається ВЗАГАЛІ — не ховається на
/// клієнті, а не потрапляє у відповідь. Сховане на клієнті знаходять за
/// секунду, переглянувши те, що прийшло з сервера, і відкритий номер у
/// публічному JSON збирають роботи за години.
/// </summary>
public class SellerPhoneVisibilityTests : IDisposable
{
    private const long SellerId = 1;

    private const long BuyerId = 2;

    private const string Phone = "+380671234567";

    private readonly TestDatabase database = new();

    private readonly AutoLotDbContext context;

    private readonly long listingId;

    public SellerPhoneVisibilityTests()
    {
        context = database.CreateContext();
        listingId = Seed();
    }

    [Fact]
    public async Task A_guest_does_not_get_the_number()
    {
        var details = await Details(viewerId: null);

        Assert.Null(details.Seller.PhoneNumber);
    }

    [Fact]
    public async Task A_signed_in_visitor_gets_it()
    {
        var details = await Details(BuyerId);

        Assert.Equal(Phone, details.Seller.PhoneNumber);
    }

    [Fact]
    public async Task A_seller_without_a_phone_gives_nothing_to_anyone()
    {
        var user = context.Users.Find(SellerId)!;
        user.PhoneNumber = null;
        context.SaveChanges();

        var details = await Details(BuyerId);

        Assert.Null(details.Seller.PhoneNumber);
    }

    public void Dispose()
    {
        context.Dispose();
        database.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task<Application.Listings.Dtos.ListingDetails> Details(long? viewerId)
    {
        var language = new StubLanguage();

        var mapper = new ListingMapper(
            context,
            language,
            new StubCurrentUser(viewerId),
            new GeoCatalog(context, language));

        var listing = context.Listings
            .Single(item => item.Id == listingId);

        context.Entry(listing).Reference(item => item.Seller).Load();
        context.Entry(listing).Reference(item => item.Car).Load();
        context.Entry(listing.Car).Reference(car => car.Make).Load();
        context.Entry(listing.Car).Reference(car => car.Model).Load();
        context.Entry(listing.Car).Collection(car => car.Photos).Load();
        context.Entry(listing.Car).Collection(car => car.Features).Load();

        return await mapper.ToDetailsAsync(listing, includePrivateFields: false, default);
    }

    private long Seed()
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
            PhoneNumber = Phone,
        });

        context.Users.Add(new User
        {
            Id = BuyerId,
            UserName = "buyer@example.com",
            Email = "buyer@example.com",
            DisplayName = "Покупець",
        });

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
}
