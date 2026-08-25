using AutoLot.Domain.Auctions;
using AutoLot.Domain.Enums;

namespace AutoLot.Tests.Auctions;

/// <summary>
/// Шкала кроку ставки з SPEC §4. Найважливіші тут — межі: саме на них
/// найлегше помилитися на один крок в один чи інший бік.
/// </summary>
public class BidStepTests
{
    [Theory]
    [InlineData(0, 25)]
    [InlineData(999, 25)]

    // Рівно на межі діє вже БІЛЬШИЙ крок: у SPEC написано «до 1 000» і
    // «1 000 – 5 000», тож тисяча належить другому рядку.
    [InlineData(1_000, 50)]
    [InlineData(4_999, 50)]
    [InlineData(5_000, 100)]
    [InlineData(19_999, 100)]
    [InlineData(20_000, 250)]
    [InlineData(1_000_000, 250)]
    public void Dollar_scale_follows_the_spec(decimal amount, decimal expected)
    {
        Assert.Equal(expected, BidStep.For(amount, Currency.Usd));
    }

    [Fact]
    public void Euro_uses_the_same_scale_as_the_dollar()
    {
        Assert.Equal(BidStep.For(7_000m, Currency.Usd), BidStep.For(7_000m, Currency.Eur));
    }

    [Theory]
    [InlineData(0, 1_000)]
    [InlineData(39_999, 1_000)]
    [InlineData(40_000, 2_000)]
    [InlineData(199_999, 2_000)]
    [InlineData(200_000, 5_000)]
    [InlineData(799_999, 5_000)]
    [InlineData(800_000, 10_000)]
    public void Hryvnia_has_its_own_scale(decimal amount, decimal expected)
    {
        Assert.Equal(expected, BidStep.For(amount, Currency.Uah));
    }

    [Fact]
    public void The_first_bid_equals_the_start_price()
    {
        // Поки ставок немає, перебивати нікого — крок не додається.
        Assert.Equal(5_000m, BidStep.MinimumNextBid(5_000m, hasBids: false, Currency.Usd));
    }

    [Fact]
    public void Every_later_bid_adds_a_step()
    {
        Assert.Equal(5_100m, BidStep.MinimumNextBid(5_000m, hasBids: true, Currency.Usd));
    }
}
