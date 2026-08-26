namespace AutoLot.Application.Auctions;

/// <summary>
/// Закриває торги, яким вийшов час. Викликається задачею планувальника, а на
/// старті застосунку — ще й для всіх лотів, чий час минув, поки сервер лежав.
/// </summary>
public interface IAuctionCloser
{
    /// <summary>
    /// Закриває один лот. Повертає <c>false</c>, якщо закривати не було чого:
    /// торги вже завершені або ще тривають.
    /// </summary>
    Task<bool> CloseAsync(long listingId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Закриває все, що прострочено, і повертає планувальникові решту —
    /// лоти, які ще торгуються. Потрібно на старті: розклад задач живе в
    /// пам'яті й після перезапуску зникає разом із процесом.
    /// </summary>
    Task<IReadOnlyList<PendingAuction>> CloseOverdueAndListPendingAsync(
        CancellationToken cancellationToken = default);
}

/// <summary>Лот, який ще торгується, і час, на який треба замовити закриття.</summary>
public sealed record PendingAuction(long ListingId, DateTimeOffset EndsAt);
