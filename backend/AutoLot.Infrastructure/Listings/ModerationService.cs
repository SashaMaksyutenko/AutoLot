using AutoLot.Application.Common.Abstractions;
using AutoLot.Application.Listings;
using AutoLot.Application.Listings.Dtos;
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
    ILogger<ModerationService> logger) : IModerationService
{
    /// <summary>Скільки живе схвалене оголошення до автоматичного завершення.</summary>
    private static readonly TimeSpan ListingLifetime = TimeSpan.FromDays(60);

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

        listing.Approve(clock.UtcNow, ListingLifetime);

        await dbContext.SaveChangesAsync(cancellationToken);

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
