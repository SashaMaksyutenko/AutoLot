using AutoLot.Domain.Common;
using AutoLot.Domain.Identity;

namespace AutoLot.Domain.Listings;

/// <summary>
/// Питання до продавця — і його відповідь. **Публічні**: їх бачить кожен, хто
/// відкрив оголошення, і вони стають частиною опису лота (SPEC §4).
///
/// Це не приватний чат. Для аукціону різниця принципова: покупець не оглядає
/// авто особисто, а відповідь одному майже завжди цікавить усіх, хто торгується.
/// Десять однакових питань у приватних листах — це десять втрачених учасників.
///
/// Відповідь лежить полями тут, а не окремою сутністю: у питання може бути
/// щонайбільше одна відповідь, і та лише від продавця. Окрема таблиця дала б
/// змогу виразити те, чого не буває.
/// </summary>
public sealed class ListingQuestion : Entity
{
    public long ListingId { get; set; }

    public Listing Listing { get; set; } = null!;

    public long AskerId { get; set; }

    public User Asker { get; set; } = null!;

    public string Text { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Порожня, поки продавець не відповів.</summary>
    public string? Answer { get; set; }

    public DateTimeOffset? AnsweredAt { get; set; }

    public bool IsAnswered => Answer is not null;

    /// <summary>
    /// Записує відповідь продавця. Повторний виклик дозволений — людина має
    /// право виправити щойно написане, надто коли йдеться про характеристики
    /// авто. Час при цьому оновлюється: видно, коли відповідь набула чинного
    /// вигляду.
    /// </summary>
    public void Reply(string text, DateTimeOffset now)
    {
        var trimmed = text?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            throw new DomainRuleException("Відповідь не може бути порожньою.");
        }

        Answer = trimmed;
        AnsweredAt = now;
    }
}
