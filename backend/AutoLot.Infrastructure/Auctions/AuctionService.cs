using AutoLot.Application.Auctions;
using AutoLot.Application.Auctions.Dtos;
using AutoLot.Application.Common.Abstractions;
using AutoLot.Domain.Auctions;
using AutoLot.Domain.Enums;
using AutoLot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoLot.Infrastructure.Auctions;

/// <summary>
/// Торги. Найризикованіше місце проєкту (SPEC §5): дві ставки, що прийшли
/// одночасно, не повинні пройти по одній ціні.
///
/// Захист — песимістичне блокування рядка. Простими словами: перший запит
/// каже базі «цей рядок мій, поки я не закінчу», і другий запит зупиняється
/// на цьому ж рядку доти, доки перший не завершить транзакцію. Далі другий
/// читає ВЖЕ ОНОВЛЕНУ ціну й порівнює ставку з нею, а не зі старою.
///
/// Оптимістична перевірка (прочитати, порахувати, записати з умовою) тут
/// гірша: ставки під кінець аукціону летять пачками, і половина з них
/// відскакувала б із проханням спробувати ще раз.
/// </summary>
internal sealed partial class AuctionService(
    AutoLotDbContext dbContext,
    IDateTimeProvider clock,
    IAuctionNotifier notifier,
    ILogger<AuctionService> logger) : IAuctionService
{
    /// <summary>Антиснайпінг: ставка в останню хвилину продовжує торги на хвилину (SPEC §4).</summary>
    private static readonly TimeSpan Extension = TimeSpan.FromMinutes(1);

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Не вдалося розіслати оновлення торгів для лота {ListingId}. Ставка збережена.")]
    private static partial void LogBroadcastFailed(
        ILogger logger,
        long listingId,
        Exception error);

    public async Task<AuctionDetails?> GetAsync(
        long listingId,
        long? viewerId,
        CancellationToken cancellationToken = default)
    {
        var auction = await dbContext.Auctions
            .AsNoTracking()
            .Include(item => item.Listing)
            .Include(item => item.Leader)
            .FirstOrDefaultAsync(item => item.ListingId == listingId, cancellationToken);

        if (auction is null || !IsPubliclyVisible(auction.Listing.Status))
        {
            return null;
        }

        return ToDetails(auction, viewerId);
    }

    public async Task<AuctionDetails> PlaceBidAsync(
        long listingId,
        long bidderId,
        decimal maxAmount,
        CancellationToken cancellationToken = default)
    {
        // Транзакція відкривається ДО читання: блокування рядка живе рівно
        // стільки, скільки транзакція, і поза нею не має сенсу.
        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken);

        var auction = await LockAsync(listingId, cancellationToken)
            ?? throw new AuctionNotFoundException(listingId);

        // Далі все читається вже під блокуванням — і статус, і ціна, і час.
        var listing = await dbContext.Listings
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == listingId, cancellationToken)
            ?? throw new AuctionNotFoundException(listingId);

        if (!IsPubliclyVisible(listing.Status))
        {
            throw new AuctionNotFoundException(listingId);
        }

        if (listing.SellerId == bidderId)
        {
            throw new BiddingNotAllowedException("Ставити на власний лот не можна.");
        }

        // Правила самих торгів живуть у сутності: скільки треба поставити,
        // хто лідирує, чи продовжувати час. Сервіс лише забезпечує їм
        // безпечне оточення — блокування й транзакцію.
        var bids = auction.PlaceBid(bidderId, maxAmount, clock.UtcNow, Extension);

        foreach (var bid in bids)
        {
            dbContext.Bids.Add(bid);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        // Ім'я лідера могло щойно змінитися, тож читаємо його наново.
        var leaderName = await GetDisplayNameAsync(auction.LeaderId, cancellationToken);

        await BroadcastAsync(auction, leaderName, bids, cancellationToken);

        return ToDetails(auction, bidderId, listing.SellerId, leaderName);
    }

    public async Task<IReadOnlyList<BidRecord>> GetHistoryAsync(
        long listingId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Bids
            .AsNoTracking()
            .Where(bid => bid.Auction.ListingId == listingId)
            .OrderByDescending(bid => bid.CreatedAt)
            .ThenByDescending(bid => bid.Id)
            .Select(bid => new BidRecord(
                bid.Id,
                bid.Bidder.DisplayName,
                bid.Amount,
                bid.IsAutomatic,
                bid.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Читає рядок торгів і одразу бере його під блокування.
    ///
    /// «FOR UPDATE» — це частина SQL, якої в LINQ немає, тому запит написаний
    /// текстом. Він каже PostgreSQL: поки транзакція не завершиться, ніхто
    /// інший цей рядок не змінить і навіть не прочитає його для зміни —
    /// чекатиме в черзі.
    ///
    /// Назва таблиці зашита в текст запиту, і це єдине місце в проєкті, де
    /// так доводиться робити; якщо таблицю перейменують, правити тут.
    /// </summary>
    private async Task<Auction?> LockAsync(long listingId, CancellationToken cancellationToken)
    {
        // ToListAsync, а не FirstOrDefaultAsync: EF не заглядає всередину
        // написаного вручну SQL, не бачить там умови й попереджає, що результат
        // «непередбачуваний». Умова там є, а рядок і поготів один — listing_id
        // унікальний. Беремо список і дістаємо з нього єдиний елемент; так
        // запит лишається тим самим, а хибне попередження зникає.
        var rows = await dbContext.Auctions
            .FromSql($"SELECT * FROM auctions WHERE listing_id = {listingId} FOR UPDATE")
            .ToListAsync(cancellationToken);

        return rows.Count > 0 ? rows[0] : null;
    }

    /// <summary>
    /// Повідомляє глядачів про нову ціну. Робиться ПІСЛЯ коміту: поки
    /// транзакція не завершена, іншим ця ставка ще не видима, і розсилати
    /// новину, якої немає в базі, було б брехнею.
    ///
    /// Збій розсилки не має скасовувати ставку — вона вже прийнята й
    /// збережена. Тому виняток тут ловиться й лише пишеться в лог: гірше,
    /// що станеться, — глядачі побачать нову ціну після наступної ставки
    /// або оновлення сторінки.
    /// </summary>
    private async Task BroadcastAsync(
        Auction auction,
        string? leaderName,
        IReadOnlyList<Bid> bids,
        CancellationToken cancellationToken)
    {
        // У рядках може бути двоє різних людей — претендент і той, чия
        // автоставка відбилася. Тягнемо імена обох одним запитом.
        var bidderIds = bids.Select(bid => bid.BidderId).Distinct().ToList();

        var names = await dbContext.Users
            .AsNoTracking()
            .Where(user => bidderIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.DisplayName, cancellationToken);

        var records = bids
            .OrderByDescending(bid => bid.Id)
            .Select(bid => new BidRecord(
                bid.Id,
                names.GetValueOrDefault(bid.BidderId, string.Empty),
                bid.Amount,
                bid.IsAutomatic,
                bid.CreatedAt))
            .ToList();

        try
        {
            await notifier.BidPlacedAsync(ToUpdate(auction, leaderName, records), cancellationToken);
        }
        catch (Exception error)
        {
            LogBroadcastFailed(logger, auction.ListingId, error);
        }
    }

    private async Task<string?> GetDisplayNameAsync(long? userId, CancellationToken cancellationToken)
    {
        if (userId is not { } id)
        {
            return null;
        }

        return await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == id)
            .Select(user => user.DisplayName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>Торги видно там само, де й саме оголошення.</summary>
    private static bool IsPubliclyVisible(ListingStatus status)
    {
        return status is ListingStatus.Active or ListingStatus.Sold;
    }

    private AuctionDetails ToDetails(Auction auction, long? viewerId)
    {
        return ToDetails(auction, viewerId, auction.Listing.SellerId, auction.Leader?.DisplayName);
    }

    private AuctionDetails ToDetails(
        Auction auction,
        long? viewerId,
        long sellerId,
        string? leaderName)
    {
        var canBid = viewerId is { } id
            && id != sellerId
            && auction.Status == AuctionStatus.Active;

        return new AuctionDetails(
            auction.ListingId,
            auction.Currency,
            auction.StartPrice,
            auction.CurrentPrice,
            auction.MinimumNextBid,
            BidStep.For(auction.CurrentPrice, auction.Currency),
            auction.BidCount,
            auction.StartsAt,
            auction.EndsAt,
            auction.Status,
            auction.ReservePrice is not null,
            auction.IsReserveMet,
            leaderName,
            viewerId is { } viewer && auction.LeaderId == viewer,
            canBid,
            auction.LeaderId,
            clock.UtcNow);
    }

    /// <summary>
    /// Те саме, але без нічого особистого: розсилка йде всім глядачам одразу,
    /// тож «чи лідирую я» кожен добудовує в себе сам.
    /// </summary>
    private AuctionUpdate ToUpdate(Auction auction, string? leaderName, IReadOnlyList<BidRecord> newBids)
    {
        return new AuctionUpdate(
            auction.ListingId,
            auction.CurrentPrice,
            auction.MinimumNextBid,
            BidStep.For(auction.CurrentPrice, auction.Currency),
            auction.BidCount,
            auction.EndsAt,
            auction.Status,
            auction.IsReserveMet,
            auction.LeaderId,
            leaderName,
            newBids,
            clock.UtcNow);
    }
}
