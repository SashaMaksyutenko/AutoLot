using AutoLot.Domain.Common;
using AutoLot.Domain.Identity;

namespace AutoLot.Domain.Chat;

/// <summary>
/// Одне повідомлення в розмові.
///
/// Прочитання позначається часом, а не прапорцем: «коли прочитали» відповідає
/// і на питання «чи прочитали», а зворотне неправда. Коштує це той самий
/// стовпець.
/// </summary>
public sealed class Message : Entity
{
    public long ConversationId { get; set; }

    public Conversation Conversation { get; set; } = null!;

    public long SenderId { get; set; }

    public User Sender { get; set; } = null!;

    public string Text { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Порожній, поки співрозмовник не відкрив розмову.</summary>
    public DateTimeOffset? ReadAt { get; set; }

    public bool IsRead => ReadAt is not null;

    /// <summary>
    /// Позначає прочитаним. Повторний виклик нічого не змінює: час має
    /// лишитися тим, коли повідомлення побачили ВПЕРШЕ.
    /// </summary>
    public void MarkRead(DateTimeOffset now)
    {
        ReadAt ??= now;
    }
}
