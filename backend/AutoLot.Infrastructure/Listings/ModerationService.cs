using AutoLot.Application.Auctions;
using AutoLot.Application.Common.Abstractions;
using AutoLot.Application.Listings;
using AutoLot.Application.Listings.Dtos;
using AutoLot.Domain.Auctions;
using AutoLot.Domain.Enums;
using AutoLot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoLot.Infrastructure.Listings;

/// <summary>
/// Розгляд оголошень модератором. Рішення пишемо в лог: за SPEC §8 дії
/// модерації підлягають аудиту, і поки повноцінного аудит-логу немає,
/// слід має лишатися хоча б тут.
/// </summary>
internal sealed partial class ModerationService(
    AutoLotDbContext dbContext,
    IDateTimeProvider clock,
    ListingMapper mapper,
    IAuctionScheduler scheduler,
    ILogger<ModerationService> logger) : IModerationService
{
    /// <summary>Скільки живе схвалене оголошення до автоматичного завершення.</summary>
    private static readonly TimeSpan ListingLifetime = TimeSpan.FromDays(60);

    /// <summary>
    /// Скільки тривають торги (SPEC §4). Це НЕ той самий строк, що вище:
    /// звичайне оголошення висить два місяці, а аукціон має бути подією
    /// з відчутним фіналом, інакше ніхто не стежитиме за ним щодня.
    /// </summary>
    private static readonly TimeSpan AuctionDuration = TimeSpan.FromDays(7);

    public async Task<IReadOnlyList<ListingSummary>> GetQueueAsync(
        CancellationToken cancellationToken = default)
    {
        // Найдавніші першими: хто подав раніше, той раніше й отримає рішення.
        var query = dbContext.Listings
            .Where(listing => listing.Status == ListingStatus.PendingModeration)
            .OrderBy(listing => listing.UpdatedAt ?? listing.CreatedAt);

        return await mapper.ToSummariesAsync(query, cancellationToken);
    }

    public async Task ApproveAsync(
        long listingId,
        long moderatorId,
        CancellationToken cancellationToken = default)
    {
        var listing = await LoadAsync(listingId, cancellationToken);

        var now = clock.UtcNow;

        listing.Approve(now, ListingLifetime);

        var auctionEndsAt = await StartAuctionIfNeededAsync(listing, now, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        // Задачу закриття замовляємо ПІСЛЯ збереження: до коміту торгів
        // у базі ще немає, і задача, яка встигла б спрацювати, нічого б
        // не знайшла.
        if (auctionEndsAt is { } endsAt)
        {
            await scheduler.ScheduleCloseAsync(listing.Id, endsAt, cancellationToken);
        }

        LogApproved(logger, listingId, moderatorId);
    }

    public async Task RejectAsync(
        long listingId,
        long moderatorId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var listing = await LoadAsync(listingId, cancellationToken);

        listing.Reject(reason.Trim());

        await dbContext.SaveChangesAsync(cancellationToken);

        LogRejected(logger, listingId, moderatorId, reason);
    }

    /// <summary>
    /// Торги стартують у момент схвалення — саме тому лот і проходить
    /// модерацію перед стартом (SPEC §4). Стартова ціна береться з ціни
    /// оголошення, резерв — із поля, яке заповнив продавець.
    ///
    /// Повторне схвалення (наприклад, лот повернули з архіву) нових торгів
    /// не створює: інакше вже зроблені ставки лишилися б у старих.
    /// </summary>
    /// <returns>Час завершення нових торгів або null, якщо створювати не було чого.</returns>
    private async Task<DateTimeOffset?> StartAuctionIfNeededAsync(
        Domain.Listings.Listing listing,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (listing.Type != ListingType.Auction)
        {
            return null;
        }

        var alreadyStarted = await dbContext.Auctions
            .AsNoTracking()
            .AnyAsync(auction => auction.ListingId == listing.Id, cancellationToken);

        if (alreadyStarted)
        {
            return null;
        }

        var endsAt = now.Add(AuctionDuration);

        dbContext.Auctions.Add(new Auction
        {
            ListingId = listing.Id,
            Currency = listing.Currency,
            StartPrice = listing.Price,
            CurrentPrice = listing.Price,
            ReservePrice = listing.ReservePrice,
            StartsAt = now,
            EndsAt = endsAt,
            Status = AuctionStatus.Active,
        });

        return endsAt;
    }

    private async Task<Domain.Listings.Listing> LoadAsync(
        long listingId,
        CancellationToken cancellationToken)
    {
        var listing = await dbContext.Listings
            .FirstOrDefaultAsync(item => item.Id == listingId, cancellationToken);

        return listing ?? throw new ListingNotFoundException(listingId);
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Оголошення {ListingId} схвалено модератором {ModeratorId}")]
    private static partial void LogApproved(ILogger logger, long listingId, long moderatorId);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Оголошення {ListingId} відхилено модератором {ModeratorId}: {Reason}")]
    private static partial void LogRejected(
        ILogger logger,
        long listingId,
        long moderatorId,
        string reason);
}
