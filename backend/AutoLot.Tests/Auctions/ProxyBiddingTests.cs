using AutoLot.Domain.Auctions;
using AutoLot.Domain.Common;
using AutoLot.Domain.Enums;

namespace AutoLot.Tests.Auctions;

/// <summary>
/// Автоставка (SPEC §4). Учасник називає стелю, система торгується за нього.
/// Уся логіка живе в сутності, тож база й сервіси тут не потрібні.
/// </summary>
public class ProxyBiddingTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan Extension = TimeSpan.FromMinutes(1);

    private const long Anna = 1;

    private const long Borys = 2;

    private const long Denys = 3;

    [Fact]
    public void The_first_bidder_pays_only_the_start_price()
    {
        var auction = Auction();

        // Анна готова віддати 8 000, але суперників немає — платити більше
        // за стартову ціну немає за що.
        var bids = auction.PlaceBid(Anna, maxAmount: 8_000m, Now, Extension);

        Assert.Equal(5_000m, auction.CurrentPrice);
        Assert.Equal(Anna, auction.LeaderId);
        Assert.Single(bids);
        Assert.False(bids[0].IsAutomatic);
    }

    [Fact]
    public void A_lower_ceiling_loses_and_only_pushes_the_price_up()
    {
        var auction = Auction();
        auction.PlaceBid(Anna, maxAmount: 8_000m, Now, Extension);

        var bids = auction.PlaceBid(Borys, maxAmount: 6_000m, Now, Extension);

        // Анна лишається лідером: її стеля вища. Ціна піднялася рівно
        // настільки, щоб перебити Бориса, — 6 000 плюс крок сотні.
        Assert.Equal(Anna, auction.LeaderId);
        Assert.Equal(6_100m, auction.CurrentPrice);

        // У історії два рядки: ставка Бориса і автовідповідь Анни.
        Assert.Equal(2, bids.Count);
        Assert.Equal(6_000m, bids[0].Amount);
        Assert.False(bids[0].IsAutomatic);
        Assert.Equal(6_100m, bids[1].Amount);
        Assert.True(bids[1].IsAutomatic);
    }

    [Fact]
    public void A_ceiling_just_short_of_the_leader_pushes_the_price_to_their_very_top()
    {
        var auction = Auction();
        auction.PlaceBid(Anna, maxAmount: 8_000m, Now, Extension);

        // Борис не дотягнув до Анниної стелі 50 доларів. Крок сотні переступив
        // би через неї, тож автовідповідь спиняється рівно на стелі: платити
        // більше, ніж вона погоджувалася, Анна не мусить.
        var bids = auction.PlaceBid(Borys, maxAmount: 7_950m, Now, Extension);

        Assert.Equal(Anna, auction.LeaderId);
        Assert.Equal(8_000m, auction.CurrentPrice);
        Assert.Equal(8_000m, auction.LeaderMaxAmount);

        Assert.Equal(2, bids.Count);
        Assert.Equal(7_950m, bids[0].Amount);
        Assert.Equal(8_000m, bids[1].Amount);
        Assert.True(bids[1].IsAutomatic);

        // Анна вичерпала стелю, тож наступний крок уже виводить її з гри.
        Assert.Equal(8_100m, auction.MinimumNextBid);
    }

    [Fact]
    public void A_higher_ceiling_wins_without_paying_it_all()
    {
        var auction = Auction();
        auction.PlaceBid(Anna, maxAmount: 8_000m, Now, Extension);

        var bids = auction.PlaceBid(Borys, maxAmount: 12_000m, Now, Extension);

        // Борис виграв, але платить не 12 000, а стелю Анни плюс крок.
        Assert.Equal(Borys, auction.LeaderId);
        Assert.Equal(8_100m, auction.CurrentPrice);

        // Спершу видно, що автоставка Анни дійшла до своєї межі й здалася.
        Assert.Equal(2, bids.Count);
        Assert.Equal(8_000m, bids[0].Amount);
        Assert.True(bids[0].IsAutomatic);
        Assert.Equal(8_100m, bids[1].Amount);
        Assert.False(bids[1].IsAutomatic);
    }

    [Fact]
    public void The_winner_never_pays_more_than_their_ceiling()
    {
        var auction = Auction();
        auction.PlaceBid(Anna, maxAmount: 8_000m, Now, Extension);

        // Стеля Бориса вища за Аннину лише на 50 — крок сотні її перестрибнув би.
        auction.PlaceBid(Borys, maxAmount: 8_050m, Now, Extension);

        Assert.Equal(Borys, auction.LeaderId);
        Assert.Equal(8_050m, auction.CurrentPrice);
    }

    [Fact]
    public void Equal_ceilings_leave_the_earlier_bidder_in_front()
    {
        var auction = Auction();
        auction.PlaceBid(Anna, maxAmount: 8_000m, Now, Extension);

        auction.PlaceBid(Borys, maxAmount: 8_000m, Now.AddMinutes(1), Extension);

        // SPEC §4: за рівних максимумів виграє той, хто виставив свій раніше.
        Assert.Equal(Anna, auction.LeaderId);
        Assert.Equal(8_000m, auction.CurrentPrice);
    }

    [Fact]
    public void A_ceiling_below_the_minimum_is_rejected()
    {
        var auction = Auction();
        auction.PlaceBid(Anna, maxAmount: 8_000m, Now, Extension);

        // Поточна ціна 5 000, крок 100 — менше за 5 100 не приймається.
        Assert.Throws<DomainRuleException>(
            () => auction.PlaceBid(Borys, maxAmount: 5_050m, Now, Extension));
    }

    [Fact]
    public void Raising_your_own_ceiling_changes_nothing_in_public()
    {
        var auction = Auction();
        auction.PlaceBid(Anna, maxAmount: 8_000m, Now, Extension);

        var bids = auction.PlaceBid(Anna, maxAmount: 15_000m, Now, Extension);

        // Ціна та сама, історія не поповнилася — публічно не сталося нічого.
        Assert.Empty(bids);
        Assert.Equal(5_000m, auction.CurrentPrice);
        Assert.Equal(1, auction.BidCount);

        // Але стеля зросла, і це проявиться, щойно хтось спробує перебити.
        auction.PlaceBid(Borys, maxAmount: 12_000m, Now, Extension);
        Assert.Equal(Anna, auction.LeaderId);
    }

    [Fact]
    public void Lowering_your_own_ceiling_is_refused()
    {
        var auction = Auction();
        auction.PlaceBid(Anna, maxAmount: 8_000m, Now, Extension);

        Assert.Throws<DomainRuleException>(
            () => auction.PlaceBid(Anna, maxAmount: 7_000m, Now, Extension));
    }

    [Fact]
    public void Three_bidders_in_a_row_settle_on_the_highest_ceiling()
    {
        var auction = Auction();

        auction.PlaceBid(Anna, maxAmount: 8_000m, Now, Extension);
        auction.PlaceBid(Borys, maxAmount: 6_000m, Now, Extension);
        auction.PlaceBid(Denys, maxAmount: 20_000m, Now, Extension);

        Assert.Equal(Denys, auction.LeaderId);
        Assert.Equal(8_100m, auction.CurrentPrice);
    }

    // ── Резервна ціна ────────────────────────────────────────────────

    [Fact]
    public void A_lot_without_a_reserve_counts_as_met_from_the_start()
    {
        var auction = Auction();

        Assert.True(auction.IsReserveMet);
    }

    [Fact]
    public void A_reserve_stays_unmet_until_the_price_reaches_it()
    {
        var auction = Auction();
        auction.ReservePrice = 10_000m;

        auction.PlaceBid(Anna, maxAmount: 8_000m, Now, Extension);

        Assert.False(auction.IsReserveMet);
    }

    // ── Антиснайпінг ─────────────────────────────────────────────────

    [Fact]
    public void A_bid_in_the_last_minute_pushes_the_finish_back()
    {
        var auction = Auction();
        auction.EndsAt = Now.AddSeconds(20);

        auction.PlaceBid(Anna, maxAmount: 8_000m, Now, Extension);

        // Двадцяти секунд, що лишалися, більше немає — попереду ціла хвилина.
        Assert.Equal(Now.AddMinutes(1), auction.EndsAt);
    }

    [Fact]
    public void An_early_bid_leaves_the_finish_alone()
    {
        var auction = Auction();
        var originalEnd = auction.EndsAt;

        auction.PlaceBid(Anna, maxAmount: 8_000m, Now, Extension);

        Assert.Equal(originalEnd, auction.EndsAt);
    }

    [Fact]
    public void Extensions_have_no_limit()
    {
        var auction = Auction();
        auction.EndsAt = Now.AddSeconds(10);

        // Десять ставок поспіль у останню секунду — і торги щоразу тривають.
        var moment = Now;

        for (var i = 0; i < 10; i++)
        {
            moment = auction.EndsAt.AddSeconds(-1);
            auction.PlaceBid(i % 2 == 0 ? Anna : Borys, 8_000m + (i * 1_000m), moment, Extension);
        }

        Assert.Equal(moment.AddMinutes(1), auction.EndsAt);
    }

    // ── Межі участі ──────────────────────────────────────────────────

    [Fact]
    public void Bidding_after_the_finish_is_refused()
    {
        var auction = Auction();

        Assert.Throws<DomainRuleException>(
            () => auction.PlaceBid(Anna, maxAmount: 8_000m, auction.EndsAt, Extension));
    }

    [Fact]
    public void Bidding_on_a_closed_auction_is_refused()
    {
        var auction = Auction();
        auction.Close();

        Assert.Throws<DomainRuleException>(
            () => auction.PlaceBid(Anna, maxAmount: 8_000m, Now, Extension));
    }

    private static Auction Auction()
    {
        return new Auction
        {
            Id = 1,
            Currency = Currency.Usd,
            StartPrice = 5_000m,
            CurrentPrice = 5_000m,
            StartsAt = Now,
            EndsAt = Now.AddDays(7),
            Status = AuctionStatus.Active,
        };
    }
}
