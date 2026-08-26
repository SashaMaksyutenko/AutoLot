using AutoLot.Domain.Auctions;
using AutoLot.Domain.Common;
using AutoLot.Domain.Enums;

namespace AutoLot.Tests.Auctions;

/// <summary>
/// Підбиття підсумків торгів. Логіка живе в самій сутності, тож перевіряється
/// без бази й планувальника — достатньо об'єкта в пам'яті.
/// </summary>
public class AuctionClosingTests
{
    private static readonly DateTimeOffset Start = new(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset Finish = Start.AddDays(7);

    private static readonly TimeSpan Extension = TimeSpan.FromMinutes(1);

    private const long Anna = 1;

    [Fact]
    public void A_lot_without_bids_ends_without_a_winner()
    {
        var auction = Auction();

        Assert.True(auction.Close(Finish));

        Assert.Equal(AuctionStatus.Ended, auction.Status);
        Assert.Null(auction.WinnerId);
    }

    [Fact]
    public void The_highest_bidder_wins_when_there_is_no_reserve()
    {
        var auction = Auction();
        auction.PlaceBid(Anna, maxAmount: 8_000m, Start, Extension);

        auction.Close(Finish);

        Assert.Equal(Anna, auction.WinnerId);
    }

    [Fact]
    public void The_highest_bidder_wins_when_the_reserve_is_reached()
    {
        var auction = Auction(reserve: 6_000m);

        // Щоб ціна дотягнула до резерву, високої стелі мало — потрібен
        // суперник, який змусить автоставку піднятися. Анна доводить ціну до
        // 6 500, Борис перебиває на крок: 6 600, і це вже понад резерв.
        auction.PlaceBid(Anna, maxAmount: 6_500m, Start, Extension);
        auction.PlaceBid(2, maxAmount: 9_000m, Start, Extension);

        auction.Close(Finish);

        Assert.Equal(6_600m, auction.CurrentPrice);
        Assert.True(auction.IsReserveMet);
        Assert.Equal(2, auction.WinnerId);
    }

    [Fact]
    public void A_high_ceiling_alone_does_not_reach_the_reserve()
    {
        var auction = Auction(reserve: 6_000m);

        auction.PlaceBid(Anna, maxAmount: 5_500m, Start, Extension);

        // Борис готовий на 9 000 — значно більше за резерв. Але переплачувати
        // немає за що: досить перебити стелю Анни на крок, і ціна спиняється
        // на 5 600. Резерв міряють ВИДИМОЮ ціною, а не чиєюсь готовністю.
        auction.PlaceBid(2, maxAmount: 9_000m, Start, Extension);

        Assert.Equal(5_600m, auction.CurrentPrice);

        auction.Close(Finish);

        Assert.False(auction.IsReserveMet);
        Assert.Null(auction.WinnerId);
    }

    [Fact]
    public void Nobody_wins_when_the_reserve_is_not_reached()
    {
        var auction = Auction(reserve: 20_000m);
        auction.PlaceBid(Anna, maxAmount: 8_000m, Start, Extension);

        auction.Close(Finish);

        // Лідер за ставками є, переможця немає: продавець від початку сказав,
        // за скільки згоден віддати авто (SPEC §4).
        Assert.Equal(Anna, auction.LeaderId);
        Assert.Null(auction.WinnerId);
        Assert.False(auction.IsReserveMet);
    }

    [Fact]
    public void Closing_twice_changes_nothing()
    {
        var auction = Auction();
        auction.PlaceBid(Anna, maxAmount: 8_000m, Start, Extension);

        Assert.True(auction.Close(Finish));

        // Друге закриття — не помилка: задача планувальника може спрацювати
        // двічі після перезапуску або на другому сервері.
        Assert.False(auction.Close(Finish));
        Assert.Equal(Anna, auction.WinnerId);
    }

    [Fact]
    public void Closing_before_the_finish_is_refused()
    {
        var auction = Auction();

        // Захист від задачі, що лишилася від старого розкладу: антиснайпінг
        // міг відсунути фінал уже після того, як її замовили.
        Assert.Throws<DomainRuleException>(() => auction.Close(Start.AddDays(1)));
    }

    [Fact]
    public void A_bid_in_the_last_minute_delays_the_closing_too()
    {
        var auction = Auction();

        // Ставка за півхвилини до кінця відсуває фінал — і тепер закривати
        // за старим часом уже не можна.
        auction.PlaceBid(Anna, maxAmount: 8_000m, Finish.AddSeconds(-30), Extension);

        Assert.Throws<DomainRuleException>(() => auction.Close(Finish));
        Assert.True(auction.Close(auction.EndsAt));
    }

    private static Auction Auction(decimal? reserve = null) => new()
    {
        Id = 1,
        Currency = Currency.Usd,
        StartPrice = 5_000m,
        CurrentPrice = 5_000m,
        ReservePrice = reserve,
        StartsAt = Start,
        EndsAt = Finish,
        Status = AuctionStatus.Active,
    };
}
