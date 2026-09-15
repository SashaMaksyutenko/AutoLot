using AutoLot.Domain.Auctions;
using AutoLot.Domain.Enums;

namespace AutoLot.Infrastructure.Listings;

/// <summary>
/// Правила, за якими живуть демонстраційні торги: коли починаються, коли
/// закінчуються, звідки береться історія ставок і як лот запускають наново.
///
/// Винесено з <see cref="DemoDataSeeder"/> окремо з двох причин. По-перше,
/// сідер відповідає за роботу з базою, а тут — чиста робота з об'єктами в
/// пам'яті: жодного запиту, жодного контексту. По-друге, саме ці правила й
/// варто перевіряти тестами, а перевірити щось, заховане всередині сідера з
/// сімома залежностями, було б непросто.
/// </summary>
internal static class DemoAuctions
{
    /// <summary>
    /// На скільки ставка під кінець відсуває фінал. Те саме значення, що в
    /// AuctionService: демо-торги мають поводитися так само, як справжні.
    /// </summary>
    private static readonly TimeSpan Extension = TimeSpan.FromMinutes(1);

    /// <summary>Кожен який лот лишається зовсім без ставок.</summary>
    private const int SilentLotEvery = 4;

    /// <summary>
    /// Розставляє строки торгів ВІД ПОТОЧНОГО МОМЕНТУ.
    ///
    /// Початок відсуваємо в минуле, щоб історії ставок було де розміститися:
    /// лот, який щойно стартував і вже має п'ять ставок, виглядав би підробкою.
    /// Кінець розкидаємо від кількох годин до кількох днів — так на сторінці
    /// водночас видно і майже дотліле, і свіже.
    /// </summary>
    public static void Schedule(Auction auction, Random random, DateTimeOffset now)
    {
        auction.StartsAt = now.AddHours(-random.Next(6, 24 * 4));
        auction.EndsAt = now.AddHours(random.Next(2, 24 * 5));
    }

    /// <summary>
    /// Запускає торги наново разом з їхнім оголошенням.
    ///
    /// Навіщо це потрібно: строк лота відраховується від моменту, коли його
    /// виставили, а демо-база живе місяцями. Через тиждень після наповнення
    /// всі лоти закриті, оголошення пішли в архів — і головна відмінність
    /// проєкту зникає з очей.
    ///
    /// Старі ставки прибираємо: вони стосувалися вже завершених торгів, і
    /// лишати їх означало б показувати ціну, якої зараз ніхто не пропонує.
    /// </summary>
    public static void Restart(Auction auction, Random random, DateTimeOffset now)
    {
        /*
            Очищення колекції саме собою видаляє ставки з бази: зв'язок
            «ставка → торги» налаштований на каскад, і ставка без своїх торгів
            існувати не може. EF це бачить і ставить рядкам позначку «видалити»,
            щойно вони зникають із колекції.
        */
        auction.Bids.Clear();

        auction.Status = AuctionStatus.Active;
        auction.WinnerId = null;
        auction.CurrentPrice = auction.StartPrice;
        auction.LeaderId = null;
        auction.LeaderMaxAmount = null;
        auction.BidCount = 0;

        Schedule(auction, random, now);

        /*
            Оголошення теж треба повернути у видачу: закриті торги відправили
            його в архів або позначили проданим.

            Стан виставляємо полем, а не методами життєвого циклу (Restore,
            Approve): правильний шлях назад — «архів → чернетка → модерація →
            публікація», і для вигаданих даних це три зайві кроки. Демо-дані
            так само оминають модерацію при створенні.
        */
        var listing = auction.Listing;

        listing.Status = ListingStatus.Active;
        listing.SoldAt = null;
        listing.BuyerId = null;
        listing.PublishedAt ??= auction.StartsAt;
        listing.ExpiresAt = auction.EndsAt.AddDays(7);
    }

    /// <summary>
    /// Набиває історію ставок.
    ///
    /// Ставки йдуть через ту саму <see cref="Auction.PlaceBid"/>, що й у
    /// справжніх торгах, — інакше довелося б вручну тримати узгодженими ціну,
    /// лідера, його стелю й лічильник, і будь-яка помилка тут виглядала б як
    /// помилка аукціону.
    ///
    /// Правила майданчика (депозит, заборона ставити на власний лот) перевіряє
    /// сервіс, а не сутність. Продавця лота ми відсіюємо самі; депозит
    /// демо-дані оминають так само, як оминають модерацію.
    /// </summary>
    /// <param name="bidderIds">Кандидати в учасники; продавця лота серед них шукати не треба.</param>
    public static void AddBids(
        Auction auction,
        IReadOnlyList<long> bidderIds,
        Random random,
        DateTimeOffset now)
    {
        // Частина лотів лишається без ставок: щойно виставлені теж мають
        // траплятися, інакше «ще ніхто не ставив» ніде не побачиш.
        if (random.Next(SilentLotEvery) == 0)
        {
            return;
        }

        var bidders = bidderIds
            .Where(id => id != auction.Listing.SellerId)
            .ToList();

        if (bidders.Count == 0)
        {
            return;
        }

        var rounds = random.Next(1, 6);

        for (var round = 0; round < rounds; round++)
        {
            var bidder = bidders[random.Next(bidders.Count)];

            // Лідер, що перебиває сам себе, лише піднімає власну стелю — в
            // історії такий хід не видно, тож користі з нього тут нема.
            if (auction.LeaderId == bidder)
            {
                continue;
            }

            // Стеля — трохи вище за найменшу прийнятну ставку. Відсоток
            // випадковий, щоб ціни не росли однаковими сходинками.
            var ceiling = decimal.Round(auction.MinimumNextBid * (1 + (random.Next(2, 18) / 100m)), 0);

            foreach (var bid in auction.PlaceBid(bidder, ceiling, MomentOf(auction, round, rounds, now), Extension))
            {
                auction.Bids.Add(bid);
            }
        }
    }

    /// <summary>
    /// Коли саме поставили. Час розподіляємо між початком торгів і «зараз»,
    /// з кожним колом ближче до теперішнього: історія має читатися згори вниз,
    /// а не стояти вся однією позначкою часу.
    ///
    /// Множення проміжку на дріб — звичайна дія над TimeSpan: «шоста частина
    /// двох діб» це рівно те, що потрібно, і рахувати години вручну не треба.
    /// </summary>
    private static DateTimeOffset MomentOf(Auction auction, int round, int rounds, DateTimeOffset now)
    {
        var elapsed = now - auction.StartsAt;

        return auction.StartsAt + (elapsed * ((round + 1) / (double)(rounds + 1)));
    }
}
