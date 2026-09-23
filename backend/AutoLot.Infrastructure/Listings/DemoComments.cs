using AutoLot.Domain.Auctions;
using AutoLot.Domain.Listings;

namespace AutoLot.Infrastructure.Listings;

/// <summary>
/// Вигадує розмову під демонстраційним лотом.
///
/// Без неї блок коментарів на кожному лоті стояв би порожнім — рівно та сама
/// біда, що колись була з порожніми аукціонами, відсутніми VIN і графіками
/// цін: можливість зроблена, а побачити її ніде.
///
/// Репліки беруться з demo-comments.json: покупці пишуть з одного набору,
/// продавець відповідає з другого. Так розмова читається як розмова.
/// </summary>
internal static class DemoComments
{
    /// <summary>Кожен який лот лишається без розмови: щойно виставлені теж мають траплятися.</summary>
    private const int QuietEvery = 4;

    private const int MaxComments = 5;

    /// <summary>Як часто наступну репліку пише продавець — відповідаючи на попереднє.</summary>
    private const int SellerRepliesEvery = 3;

    /// <summary>
    /// Чи цей лот мовчить навмисно.
    /// </summary>
    /// <remarks>
    /// Вирішує номер лота, а не випадок, — і в цьому весь сенс. Правило, яке
    /// щоразу дає ту саму відповідь, дозволяє сідеру просто спитати «кому з
    /// тих, хто має розмовляти, розмови бракує?» і дописати лише їм. Із
    /// випадком так не можна: мовчазний лот при кожному перезапуску мав би
    /// новий шанс заговорити, і врешті заговорили б усі.
    ///
    /// Номери аукціонних лотів розкидані по всьому каталогу, тож і мовчазні
    /// трапляються впереміш, а не рядком.
    /// </remarks>
    public static bool IsQuiet(long listingId) => listingId % QuietEvery == 0;

    /// <param name="sellerId">Автор оголошення — його репліки підуть із набору продавця.</param>
    /// <param name="buyers">Хто може писати як покупець; продавця серед них шукати не треба.</param>
    /// <param name="from">Коли почалися торги: раніше розмови бути не могло.</param>
    public static List<AuctionComment> Create(
        Listing listing,
        long sellerId,
        IReadOnlyList<long> buyers,
        DemoCommentsDocument lines,
        Random random,
        DateTimeOffset from,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(listing);
        ArgumentNullException.ThrowIfNull(buyers);
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(random);

        var others = buyers.Where(id => id != sellerId).ToList();

        if (IsQuiet(listing.Id) || others.Count == 0 || lines.Buyers.Count == 0)
        {
            return [];
        }

        var count = random.Next(1, MaxComments + 1);
        var comments = new List<AuctionComment>(count);
        var interval = (now - from) / (count + 1);

        for (var index = 0; index < count; index++)
        {
            /*
                Перша репліка — завжди від покупця: продавець не починає
                розмову сам із собою. Далі він час від часу відповідає, якщо
                йому є що відповісти.
            */
            var bySeller = index > 0
                && lines.Sellers.Count > 0
                && random.Next(SellerRepliesEvery) == 0;

            comments.Add(new AuctionComment
            {
                Listing = listing,
                AuthorId = bySeller ? sellerId : others[random.Next(others.Count)],
                Text = bySeller
                    ? lines.Sellers[random.Next(lines.Sellers.Count)]
                    : lines.Buyers[random.Next(lines.Buyers.Count)],

                // Рівні проміжки від початку торгів до «зараз»: розмова йде
                // упродовж торгів, а не вся однією хвилиною.
                CreatedAt = from + (interval * (index + 1)),
            });
        }

        return comments;
    }
}

/// <summary>Опис файла demo-comments.json. Внутрішній — поза демо-даними не потрібен.</summary>
internal sealed class DemoCommentsDocument
{
    public List<string> Buyers { get; init; } = [];

    public List<string> Sellers { get; init; } = [];
}
