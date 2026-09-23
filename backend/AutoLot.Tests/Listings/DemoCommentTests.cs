using AutoLot.Domain.Auctions;
using AutoLot.Domain.Enums;
using AutoLot.Domain.Listings;
using AutoLot.Infrastructure.Listings;
using AutoLot.Infrastructure.Persistence;

namespace AutoLot.Tests.Listings;

/// <summary>
/// Розмова під демонстраційними лотами.
///
/// Вимога та сама, що й до решти демо-даних: вона має виглядати як справжня
/// розмова. Продавець не починає її сам із собою, репліки йдуть у часі, а не
/// всі однією хвилиною, і жодна не з'являється раніше за самі торги.
/// </summary>
public class DemoCommentTests
{
    private const string ResourceName = "AutoLot.Infrastructure.Persistence.SeedData.demo-comments.json";

    private const long SellerId = 1;

    private static readonly long[] Everyone = [SellerId, 2, 3, 4, 5];

    private static readonly DateTimeOffset Now = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset Start = Now.AddDays(-2);

    private static readonly DemoCommentsDocument Lines =
        SeedResource.ReadAsync<DemoCommentsDocument>(ResourceName).GetAwaiter().GetResult();

    [Fact]
    public void The_file_has_lines_for_both_sides()
    {
        Assert.NotEmpty(Lines.Buyers);
        Assert.NotEmpty(Lines.Sellers);
    }

    /// <summary>
    /// Кожна репліка має вміщатися в межу, яку сервер ставить справжнім
    /// коментарям. Інакше демо-дані показували б те, чого людина написати
    /// не змогла б.
    /// </summary>
    [Fact]
    public void Every_line_fits_the_real_limit()
    {
        Assert.All(Lines.Buyers.Concat(Lines.Sellers), line =>
        {
            Assert.False(string.IsNullOrWhiteSpace(line));
            Assert.True(line.Length <= AuctionComment.MaxLength, line);
        });
    }

    [Fact]
    public void The_seller_never_speaks_first()
    {
        Assert.All(Conversations().Where(talk => talk.Count > 0), talk =>
            Assert.NotEqual(SellerId, talk[0].AuthorId));
    }

    /// <summary>
    /// Продавець говорить лише своїми репліками, покупці — своїми: інакше
    /// «Власник один, я» з'явилося б від випадкового перехожого.
    /// </summary>
    [Fact]
    public void Each_side_speaks_its_own_lines()
    {
        foreach (var comment in Conversations().SelectMany(talk => talk))
        {
            var pool = comment.AuthorId == SellerId ? Lines.Sellers : Lines.Buyers;

            Assert.Contains(comment.Text, pool);
        }
    }

    [Fact]
    public void The_conversation_runs_forward_in_time_within_the_auction()
    {
        foreach (var talk in Conversations())
        {
            for (var index = 0; index < talk.Count; index++)
            {
                Assert.InRange(talk[index].CreatedAt, Start, Now);

                if (index > 0)
                {
                    Assert.True(talk[index].CreatedAt > talk[index - 1].CreatedAt);
                }
            }
        }
    }

    /// <summary>
    /// Частина лотів мовчить навмисно, а частина розмовляє. Якби випало
    /// щось одне, демо-дані показували б лише половину картини.
    /// </summary>
    [Fact]
    public void Both_quiet_and_talkative_lots_appear()
    {
        var conversations = Conversations();

        Assert.Contains(conversations, talk => talk.Count == 0);
        Assert.Contains(conversations, talk => talk.Count > 0);
    }

    /// <summary>
    /// Мовчить лот чи розмовляє — вирішує сам лот, а не випадок. На цьому
    /// тримається повторний запуск сідера: він дописує розмову лише тим, кому
    /// вона належить, і мовчазному лоту не дає нового шансу заговорити.
    /// </summary>
    [Fact]
    public void Whether_a_lot_talks_depends_on_the_lot_not_on_luck()
    {
        foreach (var id in LotIds)
        {
            for (var seed = 0; seed < 5; seed++)
            {
                var talk = DemoComments.Create(Lot(id), SellerId, Everyone, Lines, new Random(seed), Start, Now);

                Assert.Equal(DemoComments.IsQuiet(id), talk.Count == 0);
            }
        }
    }

    [Fact]
    public void A_lot_nobody_else_can_see_stays_silent()
    {
        var talkative = LotIds.First(id => !DemoComments.IsQuiet(id));

        var talk = DemoComments.Create(Lot(talkative), SellerId, [SellerId], Lines, new Random(1), Start, Now);

        Assert.Empty(talk);
    }

    private static readonly long[] LotIds = [.. Enumerable.Range(1, 50).Select(id => (long)id)];

    // Кожен лот зі своїм зерном: так перевіряємо і різні лоти, і різний «випадок».
    private static List<List<AuctionComment>> Conversations() =>
        [.. LotIds.Select(id =>
            DemoComments.Create(Lot(id), SellerId, Everyone, Lines, new Random((int)id), Start, Now))];

    private static Listing Lot(long id) => new()
    {
        Id = id,
        SellerId = SellerId,
        Type = ListingType.Auction,
        Status = ListingStatus.Active,
    };
}
