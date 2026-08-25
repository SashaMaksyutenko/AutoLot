using AutoLot.Application.Auctions.Dtos;

namespace AutoLot.Application.Auctions;

/// <summary>
/// Торги. Адресуються через оголошення, а не через власний номер: для того,
/// хто дивиться сайт, лот і оголошення — та сама річ.
/// </summary>
public interface IAuctionService
{
    /// <summary>Стан торгів. null, якщо в оголошення їх немає або воно не публічне.</summary>
    Task<AuctionDetails?> GetAsync(
        long listingId,
        long? viewerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Поставити з автоматичним підвищенням. <paramref name="maxAmount"/> —
    /// це СТЕЛЯ, а не сума платежу: система поставить рівно стільки, скільки
    /// треба для лідерства, і сама підніматиме, поки стелі вистачає.
    /// </summary>
    Task<AuctionDetails> PlaceBidAsync(
        long listingId,
        long bidderId,
        decimal maxAmount,
        CancellationToken cancellationToken = default);

    /// <summary>Публічна історія: хто, коли й скільки, найновіші зверху.</summary>
    Task<IReadOnlyList<BidRecord>> GetHistoryAsync(
        long listingId,
        CancellationToken cancellationToken = default);
}

/// <summary>У цього оголошення немає торгів або воно недоступне.</summary>
public sealed class AuctionNotFoundException(long listingId)
    : Exception($"Торгів для оголошення {listingId} не знайдено.");

/// <summary>Ставити не можна — наприклад, це власний лот того, хто ставить.</summary>
public sealed class BiddingNotAllowedException(string message) : Exception(message);
