using AutoLot.Application.Chat.Dtos;

namespace AutoLot.Application.Chat;

/// <summary>
/// Приватне листування покупця з продавцем.
///
/// Не плутати з публічними питаннями під лотом: там відповідь бачить кожен,
/// бо стосується авто; тут — час огляду, торг, адреса.
///
/// Учасників у розмові двоє: покупець і **той бік, що продає**. Якщо лот
/// салонний, продавцем виступає будь-хто з персоналу — те саме правило, що
/// й для решти дій з оголошенням.
/// </summary>
public interface IChatService
{
    /// <summary>Мої розмови, найсвіжіші зверху.</summary>
    Task<IReadOnlyList<ConversationSummary>> GetMineAsync(
        long userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Знаходить розмову про це оголошення або починає нову. Повторний виклик
    /// повертає ту саму: одна гілка на пару «оголошення + покупець».
    /// </summary>
    Task<ConversationDetails> StartAsync(
        long listingId,
        long buyerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Стрічка розмови. Заразом позначає прочитаними чужі повідомлення —
    /// відкрити розмову й означає прочитати її.
    /// </summary>
    Task<ConversationDetails> GetAsync(
        long conversationId,
        long userId,
        CancellationToken cancellationToken = default);

    Task<MessageRecord> SendAsync(
        long conversationId,
        long senderId,
        string text,
        CancellationToken cancellationToken = default);

    /// <summary>Скільки непрочитаних усього — для позначки в шапці.</summary>
    Task<int> GetUnreadCountAsync(long userId, CancellationToken cancellationToken = default);
}

/// <summary>Розмови немає або питальник до неї не належить.</summary>
public sealed class ConversationNotFoundException(long conversationId)
    : Exception($"Розмову {conversationId} не знайдено.");

/// <summary>Листування в цьому випадку неможливе — наприклад, самому із собою.</summary>
public sealed class ChatNotAllowedException(string message) : Exception(message);
