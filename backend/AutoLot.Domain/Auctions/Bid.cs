using AutoLot.Domain.Common;
using AutoLot.Domain.Identity;

namespace AutoLot.Domain.Auctions;

/// <summary>
/// Один рядок історії торгів. Історія публічна: хто, коли й скільки поставив,
/// бачать усі — це доказ, що торги справжні (SPEC §4).
///
/// Ставку не редагують і не скасовують, тому часу зміни тут немає — лише час
/// створення.
/// </summary>
public sealed class Bid : Entity
{
    public long AuctionId { get; set; }

    public Auction Auction { get; set; } = null!;

    public long BidderId { get; set; }

    public User Bidder { get; set; } = null!;

    /// <summary>Сума, яку видно в історії.</summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Стеля автоставки, названа учасником. **Назовні не віддається ніколи** —
    /// саме її приховує правило «іншим видно лише поточну ціну». Зберігаємо
    /// заради розбору спорів: без цього неможливо довести, чому ціна змінилася
    /// саме так.
    /// </summary>
    public decimal? MaxAmount { get; set; }

    /// <summary>
    /// Ставку поставила система, а не людина: спрацювала чиясь автоставка.
    /// В інтерфейсі такі рядки позначаються бейджем.
    /// </summary>
    public bool IsAutomatic { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
