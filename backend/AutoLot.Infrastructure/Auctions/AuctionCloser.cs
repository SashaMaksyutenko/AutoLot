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
/// Завершення торгів (SPEC §5).
///
/// Захист від подвійного закриття — те саме блокування рядка, що й у ставках.
/// Окремого «розподіленого замка» не заводимо: він розв'язував би рівно ту
/// саму задачу, але вимагав би ще одного сховища й міг би розійтися з базою.
/// Тут же істина одна — рядок торгів, і хто взяв його першим, той і закриває;
/// другий дочекається своєї черги, побачить уже закриті торги й нічого не
/// зробить. Це і є ідемпотентність, якої вимагає SPEC.
/// </summary>
internal sealed partial class AuctionCloser(
    AutoLotDbContext dbContext,
    IDateTimeProvider clock,
    IAuctionNotifier notifier,
    ILogger<AuctionCloser> logger) : IAuctionCloser
{
    public async Task<bool> CloseAsync(long listingId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken);

        var auction = await LockAsync(listingId, cancellationToken);

        if (auction is null)
        {
            return false;
        }

        // Час іще не вийшов — таке буває, коли задача лишилася від старого
        // розкладу, а антиснайпінг устиг відсунути фінал.
        if (clock.UtcNow < auction.EndsAt)
        {
            LogTooEarly(logger, listingId, auction.EndsAt);
            return false;
        }

        if (!auction.Close(clock.UtcNow))
        {
            // Уже закриті: хтось нас випередив. Саме заради цього випадку
            // Close повертає прапорець, а не кидає виняток.
            return false;
        }

        await ApplyOutcomeAsync(auction, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        LogClosed(logger, listingId, auction.WinnerId, auction.CurrentPrice);

        await AnnounceAsync(auction, cancellationToken);

        return true;
    }

    public async Task<IReadOnlyList<PendingAuction>> CloseOverdueAndListPendingAsync(
        CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;

        var active = await dbContext.Auctions
            .AsNoTracking()
            .Where(auction => auction.Status == AuctionStatus.Active)
            .Select(auction => new PendingAuction(auction.ListingId, auction.EndsAt))
            .ToListAsync(cancellationToken);

        var overdue = active.Where(item => item.EndsAt <= now).ToList();

        foreach (var item in overdue)
        {
            await CloseAsync(item.ListingId, cancellationToken);
        }

        if (overdue.Count > 0)
        {
            LogOverdueClosed(logger, overdue.Count);
        }

        return [.. active.Where(item => item.EndsAt > now)];
    }

    /// <summary>
    /// Доля оголошення після торгів. Продане — коли переможець є; інакше лот
    /// іде в архів: висіти в каталозі активним він більше не має права, бо
    /// поставити на нього вже неможливо.
    /// </summary>
    private async Task ApplyOutcomeAsync(Auction auction, CancellationToken cancellationToken)
    {
        var listing = await dbContext.Listings
            .FirstOrDefaultAsync(item => item.Id == auction.ListingId, cancellationToken);

        if (listing is null || listing.Status != ListingStatus.Active)
        {
            return;
        }

        if (auction.WinnerId is not null)
        {
            // Покупця не питаємо — його визначили торги.
            listing.MarkSold(clock.UtcNow, auction.WinnerId);
        }
        else
        {
            listing.Archive();
        }
    }

    /// <summary>
    /// Повідомляє глядачів, що торги скінчилися. Як і при ставці, розсилка
    /// йде після коміту й не має права скасувати вже зроблену роботу.
    /// </summary>
    private async Task AnnounceAsync(Auction auction, CancellationToken cancellationToken)
    {
        var winnerName = auction.WinnerId is { } winnerId
            ? await dbContext.Users
                .AsNoTracking()
                .Where(user => user.Id == winnerId)
                .Select(user => user.DisplayName)
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        var outcome = new AuctionOutcome(
            auction.ListingId,
            auction.CurrentPrice,
            auction.Currency,
            auction.BidCount,
            auction.WinnerId,
            winnerName,
            auction.IsReserveMet,
            auction.EndsAt,
            clock.UtcNow);

        try
        {
            await notifier.AuctionEndedAsync(outcome, cancellationToken);
        }
        catch (Exception error)
        {
            LogAnnounceFailed(logger, auction.ListingId, error);
        }
    }

    /// <summary>Той самий прийом, що й у ставках: читаємо рядок під блокуванням.</summary>
    private async Task<Auction?> LockAsync(long listingId, CancellationToken cancellationToken)
    {
        var rows = await dbContext.Auctions
            .FromSql($"SELECT * FROM auctions WHERE listing_id = {listingId} FOR UPDATE")
            .ToListAsync(cancellationToken);

        return rows.Count > 0 ? rows[0] : null;
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Торги лота {ListingId} закрито. Переможець: {WinnerId}, ціна: {Price}.")]
    private static partial void LogClosed(
        ILogger logger,
        long listingId,
        long? winnerId,
        decimal price);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Debug,
        Message = "Закриття лота {ListingId} відкладено: торги тривають до {EndsAt}.")]
    private static partial void LogTooEarly(ILogger logger, long listingId, DateTimeOffset endsAt);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Information,
        Message = "На старті закрито прострочених торгів: {Count}.")]
    private static partial void LogOverdueClosed(ILogger logger, int count);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Warning,
        Message = "Не вдалося оголосити підсумок торгів лота {ListingId}. Торги закриті.")]
    private static partial void LogAnnounceFailed(ILogger logger, long listingId, Exception error);
}
