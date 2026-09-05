using AutoLot.Application.Analytics.Dtos;
using AutoLot.Domain.Cars;
using AutoLot.Domain.Enums;
using AutoLot.Domain.Geo;
using AutoLot.Domain.Identity;
using AutoLot.Domain.Listings;
using AutoLot.Infrastructure.Analytics;
using AutoLot.Infrastructure.Persistence;
using AutoLot.Tests.TestDoubles;

namespace AutoLot.Tests.Analytics;

/// <summary>
/// Ринкові ціни.
///
/// Найважливіше тут — поведінка на КРИХІТНІЙ вибірці. На рідкісне авто в
/// країні висить три оголошення, і «середня ціна» по них легко перетворюється
/// на вигадку. Тести стежать саме за цим: коли мовчати, коли розширювати
/// вибірку й чи завжди видно її розмір.
/// </summary>
public class PriceAnalyticsServiceTests : IDisposable
{
    private const long SellerId = 1;

    private const long GolfId = 1;

    private const long PassatId = 2;

    private readonly TestDatabase database = new();

    private readonly AutoLotDbContext context;

    public PriceAnalyticsServiceTests()
    {
        context = database.CreateContext();
        Seed();
    }

    [Fact]
    public async Task Two_listings_say_nothing()
    {
        NewListing(GolfId, 2019, 400_000m);
        NewListing(GolfId, 2019, 420_000m);

        // Дві ціни — це не ринок, а збіг. Краще промовчати, ніж вивести
        // «середню», якій ніхто не мав би вірити.
        Assert.Null(await Service().ForModelAsync(GolfId, 2019));
    }

    [Fact]
    public async Task Three_listings_are_enough()
    {
        NewListing(GolfId, 2019, 400_000m);
        NewListing(GolfId, 2019, 500_000m);
        NewListing(GolfId, 2019, 600_000m);

        var stats = await Service().ForModelAsync(GolfId, 2019);

        Assert.NotNull(stats);
        Assert.Equal(3, stats.Count);
        Assert.Equal(500_000m, stats.Median);
        Assert.Equal(500_000m, stats.Average);
        Assert.Equal(400_000m, stats.Min);
        Assert.Equal(600_000m, stats.Max);
        Assert.Equal(PriceBasis.ModelAndYear, stats.Basis);
        Assert.Equal(2019, stats.Year);
    }

    [Fact]
    public async Task The_median_ignores_one_absurd_price()
    {
        NewListing(GolfId, 2019, 400_000m);
        NewListing(GolfId, 2019, 420_000m);
        NewListing(GolfId, 2019, 440_000m);
        NewListing(GolfId, 2019, 9_000_000m);

        var stats = await Service().ForModelAsync(GolfId, 2019);

        // Заради цього медіана й головна: одне оголошення з захмарною ціною
        // зсунуло середнє більш ніж удвічі, а медіану — на п'ять відсотків.
        Assert.Equal(430_000m, stats!.Median);
        Assert.Equal(2_565_000m, stats.Average);
    }

    [Fact]
    public async Task The_median_of_an_even_count_is_the_middle_of_two()
    {
        NewListing(GolfId, 2019, 400_000m);
        NewListing(GolfId, 2019, 420_000m);
        NewListing(GolfId, 2019, 440_000m);
        NewListing(GolfId, 2019, 460_000m);

        // Інакше «медіана» стрибала б залежно від того, який із двох
        // сусідів узяти.
        Assert.Equal(430_000m, (await Service().ForModelAsync(GolfId, 2019))!.Median);
    }

    [Fact]
    public async Task Too_few_for_the_year_widens_to_the_whole_model()
    {
        NewListing(GolfId, 2019, 400_000m);
        NewListing(GolfId, 2017, 300_000m);
        NewListing(GolfId, 2015, 200_000m);

        var stats = await Service().ForModelAsync(GolfId, 2019);

        // Для рідкісного авто ширша вибірка — єдине, що взагалі можна
        // показати. Але клієнт має знати, що вона ширша.
        Assert.Equal(PriceBasis.Model, stats!.Basis);
        Assert.Null(stats.Year);
        Assert.Equal(3, stats.Count);
        Assert.Equal(300_000m, stats.Median);
    }

    [Fact]
    public async Task The_exact_year_wins_when_there_is_enough_of_it()
    {
        NewListing(GolfId, 2019, 400_000m);
        NewListing(GolfId, 2019, 500_000m);
        NewListing(GolfId, 2019, 600_000m);
        NewListing(GolfId, 2005, 90_000m);

        var stats = await Service().ForModelAsync(GolfId, 2019);

        // Старе авто іншого року не має тягнути медіану вниз, коли свого
        // року вистачає.
        Assert.Equal(PriceBasis.ModelAndYear, stats!.Basis);
        Assert.Equal(500_000m, stats.Median);
    }

    [Fact]
    public async Task Another_model_is_another_market()
    {
        NewListing(GolfId, 2019, 400_000m);
        NewListing(GolfId, 2019, 500_000m);
        NewListing(PassatId, 2019, 900_000m);
        NewListing(PassatId, 2019, 950_000m);

        // По кожній моделі окремо їх лише по дві — отже, мовчимо.
        Assert.Null(await Service().ForModelAsync(GolfId, 2019));
        Assert.Null(await Service().ForModelAsync(PassatId, 2019));
    }

    [Fact]
    public async Task Only_published_listings_count()
    {
        NewListing(GolfId, 2019, 400_000m);
        NewListing(GolfId, 2019, 500_000m);
        NewListing(GolfId, 2019, 600_000m, ListingStatus.Draft);
        NewListing(GolfId, 2019, 700_000m, ListingStatus.Archived);

        // Чернетки й архів — не ринок: за цими цінами нікому не пропонують.
        Assert.Null(await Service().ForModelAsync(GolfId, 2019));
    }

    [Fact]
    public async Task A_listing_is_compared_against_the_median()
    {
        var cheap = NewListing(GolfId, 2019, 400_000m);
        NewListing(GolfId, 2019, 500_000m);
        NewListing(GolfId, 2019, 600_000m);

        var insight = await Service().ForListingAsync(cheap);

        Assert.NotNull(insight);
        Assert.Equal(400_000m, insight.PriceUah);
        Assert.Equal(500_000m, insight.Market.Median);

        // 400 проти 500 — на двадцять відсотків дешевше.
        Assert.Equal(-20, insight.PercentFromMedian);
    }

    [Fact]
    public async Task A_dearer_listing_gets_a_positive_percent()
    {
        NewListing(GolfId, 2019, 400_000m);
        NewListing(GolfId, 2019, 500_000m);
        var dear = NewListing(GolfId, 2019, 650_000m);

        Assert.Equal(30, (await Service().ForListingAsync(dear))!.PercentFromMedian);
    }

    [Fact]
    public async Task A_listing_with_no_market_gets_nothing()
    {
        var lonely = NewListing(GolfId, 2019, 400_000m);

        // Єдине оголошення моделі не має з чим порівнюватися — і вигадувати
        // порівняння не можна.
        Assert.Null(await Service().ForListingAsync(lonely));
    }

    [Fact]
    public async Task A_listing_is_never_compared_across_years()
    {
        var old = NewListing(GolfId, 2009, 200_000m);
        NewListing(GolfId, 2020, 900_000m);
        NewListing(GolfId, 2021, 950_000m);

        // По моделі загалом вибірки вистачає — але порівнювати авто 2009 року
        // з медіаною, що змішала 2009 і 2021, означає сказати «−75%» там, де
        // насправді нічого не відомо. Краще змовчати.
        Assert.NotNull(await Service().ForModelAsync(GolfId, 2009));
        Assert.Null(await Service().ForListingAsync(old));
    }

    [Fact]
    public async Task An_unknown_listing_gets_nothing()
    {
        Assert.Null(await Service().ForListingAsync(999_999));
    }

    [Fact]
    public async Task The_names_come_along_for_the_label()
    {
        NewListing(GolfId, 2019, 400_000m);
        NewListing(GolfId, 2019, 500_000m);
        NewListing(GolfId, 2019, 600_000m);

        var stats = await Service().ForModelAsync(GolfId, 2019);

        // Без назв підпис читався б як «середня ціна моделі 1».
        Assert.Equal("Volkswagen", stats!.MakeName);
        Assert.Equal("Golf", stats.ModelName);
    }

    public void Dispose()
    {
        context.Dispose();
        database.Dispose();
        GC.SuppressFinalize(this);
    }

    private PriceAnalyticsService Service() => new(context);

    private long NewListing(
        long modelId,
        int year,
        decimal priceUah,
        ListingStatus status = ListingStatus.Active)
    {
        var listing = new Listing
        {
            Title = "Тестовий лот",
            Description = "Опис",
            SellerId = SellerId,
            CityId = 1,
            Price = priceUah,
            Currency = Currency.Uah,
            PriceUah = priceUah,
            Status = status,
            Car = new Car
            {
                Year = year,
                MakeId = 1,
                ModelId = modelId,
                FuelType = FuelType.Petrol,
                Transmission = TransmissionType.Manual,
                Drivetrain = DrivetrainType.FrontWheel,
                BodyType = BodyType.Hatchback,
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
        context.Makes.Add(new Make { Id = 1, Name = "Volkswagen", Slug = "volkswagen" });
        context.Models.Add(new Model { Id = GolfId, MakeId = 1, Name = "Golf", Slug = "golf" });
        context.Models.Add(new Model { Id = PassatId, MakeId = 1, Name = "Passat", Slug = "passat" });

        context.Users.Add(new User
        {
            Id = SellerId,
            UserName = "seller@example.com",
            Email = "seller@example.com",
            DisplayName = "Продавець",
        });

        context.SaveChanges();
    }
}
