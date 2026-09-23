using AutoLot.Application.Auctions;
using AutoLot.Application.Auctions.Dtos;
using AutoLot.Application.Common.Abstractions;
using AutoLot.Application.Listings;
using AutoLot.Domain.Auctions;
using AutoLot.Domain.Common;
using AutoLot.Domain.Enums;
using AutoLot.Infrastructure.Listings;
using AutoLot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoLot.Infrastructure.Auctions;

/// <summary>
/// Живі коментарі під лотом.
///
/// Хто пише й чи можна писати — вирішується тут, за даними з бази, а не за
/// тим, що надіслав клієнт (SPEC §8). Сама розсилка — через той самий канал,
/// що й ставки, тож окремого з'єднання браузеру не потрібно.
/// </summary>
internal sealed partial class AuctionCommentService(
    AutoLotDbContext dbContext,
    IDateTimeProvider clock,
    ListingAccess access,
    IAuctionNotifier notifier,
    ILogger<AuctionCommentService> logger) : IAuctionCommentService
{
    /// <summary>
    /// Скільки коментарів віддаємо за раз. Жива розмова під популярним лотом
    /// може розростися до сотень; старіші за сотню не читає вже ніхто.
    /// </summary>
    private const int Shown = 100;

    /// <summary>
    /// Мінімальна пауза між коментарями однієї людини під одним лотом.
    /// </summary>
    /// <remarks>
    /// Жива стрічка без жодного обмеження — запрошення засипати її однаковими
    /// рядками. Кілька секунд не заважають нормальній розмові, але роблять
    /// потік сміття повільним і помітним.
    /// </remarks>
    private static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(5);

    public async Task<IReadOnlyList<CommentRecord>> GetAsync(
        long listingId,
        CancellationToken cancellationToken = default)
    {
        var lot = await LoadLotAsync(listingId, cancellationToken);

        if (lot is null)
        {
            return [];
        }

        var comments = await dbContext.AuctionComments
            .AsNoTracking()
            .Where(comment => comment.ListingId == listingId)
            .OrderByDescending(comment => comment.CreatedAt)
            .ThenByDescending(comment => comment.Id)
            .Take(Shown)
            .Select(comment => new
            {
                comment.Id,
                comment.AuthorId,
                comment.Author.DisplayName,
                comment.Text,
                comment.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        /*
            «Продавець» — не лише автор оголошення, а й будь-хто з його салону.
            Питаємо про кожного автора один раз, а не про кожен коментар:
            у розмові ті самі люди пишуть по кілька разів.
        */
        var sellers = new HashSet<long>();

        foreach (var authorId in comments.Select(comment => comment.AuthorId).Distinct())
        {
            if (await access.CanManageAsync(lot, authorId, cancellationToken))
            {
                sellers.Add(authorId);
            }
        }

        return [.. comments.Select(comment => new CommentRecord(
            comment.Id,
            listingId,
            comment.DisplayName,
            sellers.Contains(comment.AuthorId),
            comment.Text,
            comment.CreatedAt))];
    }

    public async Task<CommentRecord> PostAsync(
        long listingId,
        long authorId,
        string text,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);

        var lot = await LoadLotAsync(listingId, cancellationToken)
            ?? throw new ListingNotFoundException(listingId);

        var now = clock.UtcNow;

        var auction = await dbContext.Auctions
            .AsNoTracking()
            .Where(item => item.ListingId == listingId)
            .Select(item => new { item.Status, item.EndsAt })
            .FirstOrDefaultAsync(cancellationToken);

        // Перевіряємо і стан, і час: планувальник закриває торги із
        // затримкою в кілька секунд, а писати «після гонгу» не можна.
        if (auction is null || auction.Status is not AuctionStatus.Active || now >= auction.EndsAt)
        {
            throw new DomainRuleException(MessageCodes.CommentAuctionClosed);
        }

        var lastByAuthor = await dbContext.AuctionComments
            .AsNoTracking()
            .Where(comment => comment.ListingId == listingId && comment.AuthorId == authorId)
            .OrderByDescending(comment => comment.CreatedAt)
            .Select(comment => (DateTimeOffset?)comment.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (lastByAuthor is { } previous && now - previous < Cooldown)
        {
            throw new DomainRuleException(MessageCodes.CommentTooSoon);
        }

        var comment = new AuctionComment
        {
            ListingId = listingId,
            AuthorId = authorId,
            Text = text.Trim(),
            CreatedAt = now,
        };

        dbContext.AuctionComments.Add(comment);
        await dbContext.SaveChangesAsync(cancellationToken);

        var authorName = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == authorId)
            .Select(user => user.DisplayName)
            .FirstAsync(cancellationToken);

        var record = new CommentRecord(
            comment.Id,
            listingId,
            authorName,
            await access.CanManageAsync(lot, authorId, cancellationToken),
            comment.Text,
            comment.CreatedAt);

        await AnnounceAsync(record, cancellationToken);

        return record;
    }

    /// <summary>
    /// Лот, під яким можна читати коментарі: з торгами й видимий стороннім.
    /// Продане лишається видимим навмисно — розмова під лотом є частиною його
    /// історії, і той, хто прийде за посиланням, має її бачити.
    /// </summary>
    private Task<Domain.Listings.Listing?> LoadLotAsync(long listingId, CancellationToken cancellationToken) =>
        dbContext.Listings
            .AsNoTracking()
            .FirstOrDefaultAsync(
                listing => listing.Id == listingId
                    && listing.Type == ListingType.Auction
                    && (listing.Status == ListingStatus.Active || listing.Status == ListingStatus.Sold),
                cancellationToken);

    /// <summary>
    /// Розсилка йде після збереження й не має права скасувати вже записаний
    /// коментар: той, хто не дочекався живої новини, побачить його, щойно
    /// оновить сторінку.
    /// </summary>
    private async Task AnnounceAsync(CommentRecord record, CancellationToken cancellationToken)
    {
        try
        {
            await notifier.CommentPostedAsync(record, cancellationToken);
        }
        catch (Exception error)
        {
            LogBroadcastFailed(logger, record.ListingId, error);
        }
    }

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Warning,
        Message = "Коментар під лотом {ListingId} збережено, але розіслати не вдалося")]
    private static partial void LogBroadcastFailed(ILogger logger, long listingId, Exception error);
}
