using AutoLot.Domain.Enums;
using AutoLot.Domain.Listings;
using AutoLot.Infrastructure.Listings;

namespace AutoLot.Tests.Listings;

/// <summary>
/// Ціни в демонстраційних даних.
///
/// Головна вимога — щоб вони виглядали як ціни, які ставлять люди. Справжній
/// продавець не пише «58 015 $»: він пише 58 000 чи 57 900. Некругла сума в
/// історії ціни видає вигадані дані з першого погляду.
/// </summary>
public class DemoPriceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);

    // ─────────────────────────── Округлення ───────────────────────────

    [Theory]
    [InlineData(58_015, 58_000)]
    [InlineData(13_992, 14_000)]
    [InlineData(1_688.73, 1_700)]
    [InlineData(42_400, 42_400)]
    public void Dollars_round_to_the_nearest_hundred(decimal price, decimal expected)
    {
        Assert.Equal(expected, DemoPrices.Round(price, Currency.Usd));
    }

    [Theory]
    [InlineData(1_267_118.17, 1_267_000)]
    [InlineData(952_560, 953_000)]
    public void Hryvnias_round_to_the_nearest_thousand(decimal price, decimal expected)
    {
        Assert.Equal(expected, DemoPrices.Round(price, Currency.Uah));
    }

    /// <summary>
    /// Рівно посередині — вгору, як округлюють люди. За замовчуванням .NET
    /// округлює «до парного», і 57 850 стало б 57 800.
    /// </summary>
    [Fact]
    public void A_price_exactly_in_the_middle_rounds_up()
    {
        Assert.Equal(57_900, DemoPrices.Round(57_850, Currency.Usd));
    }

    [Fact]
    public void A_cheap_car_never_rounds_down_to_nothing()
    {
        Assert.Equal(100, DemoPrices.Round(30, Currency.Usd));
    }

    // ─────────────────────────── Історія ───────────────────────────

    /// <summary>
    /// Кожна точка історії кругла — і та, з якої починали, і проміжні.
    /// Перевіряємо на півсотні оголошень, бо кількість змін випадкова.
    /// </summary>
    [Theory]
    [InlineData(Currency.Usd, 18_400)]
    [InlineData(Currency.Uah, 773_000)]
    public void Every_point_of_the_history_is_round(Currency currency, decimal price)
    {
        foreach (var history in Histories(currency, price))
        {
            Assert.All(history, point =>
                Assert.Equal(DemoPrices.Round(point.Price, currency), point.Price));
        }
    }

    /// <summary>
    /// Історія йде згори вниз: кожна наступна ціна нижча за попередню. І
    /// жодні дві сусідні не збігаються — інакше в історії була б «зміна»,
    /// якої не видно на графіку.
    /// </summary>
    [Fact]
    public void Each_step_goes_strictly_down()
    {
        foreach (var history in Histories(Currency.Usd, 18_400))
        {
            for (var index = 1; index < history.Count; index++)
            {
                Assert.True(
                    history[index].Price < history[index - 1].Price,
                    $"{history[index - 1].Price} → {history[index].Price}");
            }
        }
    }

    /// <summary>
    /// Найризикованіший випадок для округлення — дуже дешеве авто: зниження
    /// на 3% там менше за сотню, і після округлення сусідні ціни злилися б.
    /// </summary>
    [Fact]
    public void Even_a_cheap_car_gets_distinct_steps()
    {
        foreach (var history in Histories(Currency.Usd, 1_500))
        {
            Assert.Equal(history.Count, history.Select(point => point.Price).Distinct().Count());
        }
    }

    [Fact]
    public void The_last_point_is_the_current_price()
    {
        foreach (var history in Histories(Currency.Usd, 18_400))
        {
            Assert.Equal(18_400, history[^1].Price);
        }
    }

    [Fact]
    public void An_auction_gets_no_invented_history()
    {
        var auction = Listing(Currency.Usd, 18_400);

        auction.Type = ListingType.Auction;

        Assert.Empty(DemoPrices.Create(auction, new Random(1), Now.AddDays(-30), Now));
    }

    // ─────────────────────────── Наявна історія ───────────────────────────

    /// <summary>
    /// Історія, записана ранньою версією сідера, — саме ті числа, що були в
    /// базі: Mitsubishi Lancer 2026, чотири точки від 58 015 до 42 420 $.
    /// Після округлення лишаються ті самі чотири точки й ті самі дати.
    /// </summary>
    [Fact]
    public void Existing_history_keeps_its_shape_and_only_loses_the_odd_cents()
    {
        var dates = new[] { Now.AddDays(-40), Now.AddDays(-30), Now.AddDays(-20), Now.AddDays(-10) };
        var history = Points(Currency.Usd, dates, 58_014.91m, 51_798.13m, 47_087.34m, 42_420m);

        DemoPrices.RoundInPlace(history, current: 42_400m, Currency.Usd, rate: 42m);

        Assert.Equal([58_000m, 51_800m, 47_100m, 42_400m], history.Select(point => point.Price));
        Assert.Equal(dates, history.Select(point => point.ChangedAt));
    }

    /// <summary>
    /// Гривня йде в тисячах, і гривневий еквівалент кожної точки
    /// перераховується разом із сумою — інакше графік, який малюється саме
    /// за гривнею, розійшовся б з написаними поруч цінами.
    /// </summary>
    [Fact]
    public void Hryvnia_history_rounds_to_thousands_and_keeps_its_equivalent()
    {
        var dates = new[] { Now.AddDays(-20), Now.AddDays(-10) };
        var history = Points(Currency.Uah, dates, 1_267_118.17m, 952_560m);

        DemoPrices.RoundInPlace(history, current: 953_000m, Currency.Uah, rate: 1m);

        Assert.Equal([1_267_000m, 953_000m], history.Select(point => point.Price));
        Assert.All(history, point => Assert.Equal(point.Price, point.PriceUah));
    }

    /// <summary>
    /// Округлення не має злити дві сусідні точки в одну: дешеве авто, яке
    /// подешевшало на 40 доларів, після округлення до сотні мало б дві
    /// однакові ціни. Раніша точка тоді піднімається на крок.
    /// </summary>
    [Fact]
    public void Rounding_never_merges_two_neighbouring_prices()
    {
        var dates = new[] { Now.AddDays(-20), Now.AddDays(-10) };
        var history = Points(Currency.Usd, dates, 1_540m, 1_500m);

        DemoPrices.RoundInPlace(history, current: 1_500m, Currency.Usd, rate: 42m);

        Assert.Equal([1_600m, 1_500m], history.Select(point => point.Price));
    }

    // ─────────────────────────── Оснащення ───────────────────────────

    private static List<PriceChange> Points(
        Currency currency,
        DateTimeOffset[] dates,
        params decimal[] prices) =>
        [.. prices.Select((price, index) => new PriceChange
        {
            Price = price,
            Currency = currency,
            PriceUah = price,
            ChangedAt = dates[index],
        })];

    private static List<List<PriceChange>> Histories(Currency currency, decimal price)
    {
        var histories = new List<List<PriceChange>>();

        for (var seed = 0; seed < 50; seed++)
        {
            histories.Add(DemoPrices.Create(
                Listing(currency, price),
                new Random(seed),
                Now.AddDays(-30),
                Now));
        }

        return histories;
    }

    private static Listing Listing(Currency currency, decimal price) => new()
    {
        Type = ListingType.FixedPrice,
        Currency = currency,
        Price = price,
        PriceUah = currency is Currency.Uah ? price : price * 42,
    };
}
