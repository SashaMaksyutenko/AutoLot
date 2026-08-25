namespace AutoLot.Application.Auctions.Dtos;

/// <summary>
/// Рядок публічної історії торгів. Стелі автоставки тут немає й бути не може:
/// приховане саме верхнє обмеження, а не самі ставки (SPEC §4).
/// </summary>
public sealed record BidRecord(
    long Id,
    string BidderName,
    decimal Amount,

    /// <summary>Ставку підняла чужа автоставка, а не людина вручну.</summary>
    bool IsAutomatic,
    DateTimeOffset CreatedAt);
