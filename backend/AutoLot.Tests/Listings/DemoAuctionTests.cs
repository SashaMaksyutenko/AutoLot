using AutoLot.Domain.Auctions;
using AutoLot.Domain.Enums;
using AutoLot.Domain.Listings;
using AutoLot.Infrastructure.Listings;

namespace AutoLot.Tests.Listings;

/// <summary>
/// Правила демонстраційних торгів.
///
/// Перевіряти тут є що, попри «це ж усього лише демо-дані». Порожня вкладка
/// «Аукціони» — найпомітніша вада проєкту для того, хто відкрив його вперше,
/// а причина в тому, що строк лота відраховувався від дати наповнення бази.
/// Ці тести стежать, щоб лот після перезапуску справді був живий і щоб
/// набита історія ставок не суперечила сама собі.
/// </summary>
public class DemoAuctionTests
{
    private const long SellerId = 1;

    private static readonly DateTimeOffset Now = new(2026, 9, 15, 10, 0, 0, TimeSpan.Zero);

    /// <summary>Хто може ставити. Продавця лота тут немає навмисно — його додають окремі тести.</summary>
    private static readonly long[] Bidders = [2, 3, 4, 5];

    [Fact]
    public void A_finished_lot_comes_back_to_life()
    {
        var auction = Finished();

        DemoAuctions.Restart(auction, new Random(1), Now);

        Assert.Equal(AuctionStatus.Active, auction.Status);
        Assert.True(auction.EndsAt > Now, "торги мають закінчуватися в майбутньому");
        Assert.True(auction.StartsAt < Now, "торги мають уже йти, а не починатися щойно");
    }

    [Fact]
    public void A_restarted_lot_forgets_the_previous_round()
    {
        var auction = Finished();

        DemoAuctions.Restart(auction, new Random(1), Now);

        Assert.Equal(auction.StartPrice, auction.CurrentPrice);
        Assert.Null(auction.LeaderId);
        Assert.Null(auction.LeaderMaxAmount);
        Assert.Null(auction.WinnerId);
        Assert.Equal(0, auction.BidCount);
        Assert.Empty(auction.Bids);
    }

    /// <summary>
    /// Закриті торги відправили оголошення в архів або позначили проданим.
    /// Якщо його не повернути у видачу, лот живий, а в каталозі його немає.
    /// </summary>
    [Fact]
    public void The_listing_returns_to_the_catalogue()
    {
        var auction = Finished();

        DemoAuctions.Restart(auction, new Random(1), Now);

        var listing = auction.Listing;

        Assert.Equal(ListingStatus.Active, listing.Status);
        Assert.Null(listing.SoldAt);
        Assert.Null(listing.BuyerId);
        Assert.True(listing.ExpiresAt > auction.EndsAt, "оголошення має пережити власні торги");
    }

    [Fact]
    public void Nobody_bids_on_their_own_lot()
    {
        // Продавець серед кандидатів — саме те, що робить сідер: він передає
        // весь перелік демо-продавців, не вилучаючи власника лота.
        long[] candidates = [SellerId, .. Bidders];

        Assert.All(ManyAuctions(candidates), auction =>
            Assert.DoesNotContain(auction.Bids, bid => bid.BidderId == SellerId));
    }

    [Fact]
    public void A_lot_whose_only_candidate_is_the_seller_stays_untouched()
    {
        var auction = Running();

        DemoAuctions.AddBids(auction, [SellerId], new Random(7), Now);

        Assert.Empty(auction.Bids);
        Assert.Equal(auction.StartPrice, auction.CurrentPrice);
    }

    /// <summary>
    /// Лічильник ставок зберігається окремо від самих ставок — щоб не рахувати
    /// їх запитом щоразу. Через це вони й можуть розійтися.
    /// </summary>
    [Fact]
    public void The_counter_matches_the_history()
    {
        Assert.All(ManyAuctions(Bidders), auction =>
            Assert.Equal(auction.Bids.Count, auction.BidCount));
    }

    [Fact]
    public void The_price_never_drops_below_the_starting_one()
    {
        Assert.All(ManyAuctions(Bidders), auction =>
            Assert.True(
                auction.CurrentPrice >= auction.StartPrice,
                $"ціна {auction.CurrentPrice} нижча за стартову {auction.StartPrice}"));
    }

    [Fact]
    public void A_lot_with_bids_has_a_leader_and_a_lot_without_has_none()
    {
        Assert.All(ManyAuctions(Bidders), auction =>
            Assert.Equal(auction.Bids.Count > 0, auction.LeaderId is not null));
    }

    /// <summary>
    /// Історію показують згори вниз, від свіжого до давнього. Ставка «з
    /// майбутнього» або раніша за початок торгів зламала б цей порядок.
    /// </summary>
    [Fact]
    public void Every_bid_falls_between_the_start_and_now()
    {
        Assert.All(ManyAuctions(Bidders), auction =>
            Assert.All(auction.Bids, bid =>
            {
                Assert.True(bid.CreatedAt >= auction.StartsAt, "ставка раніша за початок торгів");
                Assert.True(bid.CreatedAt <= Now, "ставка з майбутнього");
            }));
    }

    /// <summary>
    /// Частина лотів навмисно лишається без ставок, а частина їх отримує.
    /// Якби випало щось одне, демо-дані показували б лише половину картини.
    /// </summary>
    [Fact]
    public void Both_quiet_and_busy_lots_appear()
    {
        var auctions = ManyAuctions(Bidders);

        Assert.Contains(auctions, auction => auction.Bids.Count == 0);
        Assert.Contains(auctions, auction => auction.Bids.Count > 0);
    }

    /// <summary>
    /// Ставки випадкові, тож одного прикладу замало: перевіряємо правила на
    /// півсотні лотів, зібраних різними зернами.
    /// </summary>
    private static List<Auction> ManyAuctions(IReadOnlyList<long> candidates)
    {
        var auctions = new List<Auction>(50);

        for (var seed = 0; seed < 50; seed++)
        {
            var random = new Random(seed);
            var auction = Running();

            DemoAuctions.Schedule(auction, random, Now);
            DemoAuctions.AddBids(auction, candidates, random, Now);

            auctions.Add(auction);
        }

        return auctions;
    }

    /// <summary>Лот, на якому торги щойно тривають і ставок ще немає.</summary>
    private static Auction Running() => new()
    {
        Listing = new Listing { SellerId = SellerId, Status = ListingStatus.Active },
        Currency = Currency.Usd,
        StartPrice = 12_000,
        CurrentPrice = 12_000,
        StartsAt = Now.AddDays(-2),
        EndsAt = Now.AddDays(2),
        Status = AuctionStatus.Active,
    };

    /// <summary>Лот, який уже закрився з переможцем, а оголошення пішло в продані.</summary>
    private static Auction Finished()
    {
        var auction = new Auction
        {
            Listing = new Listing
            {
                SellerId = SellerId,
                Status = ListingStatus.Sold,
                SoldAt = Now.AddDays(-30),
                BuyerId = 2,
            },
            Currency = Currency.Usd,
            StartPrice = 12_000,
            CurrentPrice = 15_400,
            LeaderId = 2,
            LeaderMaxAmount = 16_000,
            BidCount = 3,
            WinnerId = 2,
            StartsAt = Now.AddDays(-37),
            EndsAt = Now.AddDays(-30),
            Status = AuctionStatus.Ended,
        };

        auction.Bids.Add(new Bid { BidderId = 2, Amount = 15_400, CreatedAt = Now.AddDays(-31) });

        return auction;
    }
}
