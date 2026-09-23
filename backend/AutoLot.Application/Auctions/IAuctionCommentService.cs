using AutoLot.Application.Auctions.Dtos;

namespace AutoLot.Application.Auctions;

/// <summary>
/// Живі коментарі під лотом з торгами.
/// </summary>
public interface IAuctionCommentService
{
    /// <summary>
    /// Коментарі лота, свіжіші зверху. Порожньо, якщо це не лот із торгами
    /// або його не видно стороннім.
    /// </summary>
    Task<IReadOnlyList<CommentRecord>> GetAsync(
        long listingId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Додає коментар і розсилає його всім, хто зараз дивиться лот.
    /// </summary>
    /// <remarks>
    /// Писати можна лише поки торги тривають: після фіналу розмова
    /// перетворюється на обговорення результату, а це вже інша річ.
    /// </remarks>
    Task<CommentRecord> PostAsync(
        long listingId,
        long authorId,
        string text,
        CancellationToken cancellationToken = default);
}
