using AutoLot.Application.Listings.Dtos;

namespace AutoLot.Application.Listings;

/// <summary>Черга модерації. Доступна лише модераторам і адміністраторам.</summary>
public interface IModerationService
{
    Task<IReadOnlyList<ListingSummary>> GetQueueAsync(CancellationToken cancellationToken = default);

    Task ApproveAsync(long listingId, long moderatorId, CancellationToken cancellationToken = default);

    Task RejectAsync(
        long listingId,
        long moderatorId,
        string reason,
        CancellationToken cancellationToken = default);
}
