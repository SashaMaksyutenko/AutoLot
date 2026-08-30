namespace AutoLot.Application.Chat.Dtos;

/// <summary>
/// Рядок списку розмов.
///
/// «Співрозмовник» тут відносний: для покупця це продавець, для продавця —
/// покупець. Обчислює це сервер, бо лише він знає, з якого боку дивиться
/// той, хто питає.
/// </summary>
public sealed record ConversationSummary(
    long Id,
    long ListingId,
    string ListingTitle,
    string? ListingPhotoPath,
    string CompanionName,
    string? LastMessageText,
    DateTimeOffset LastMessageAt,
    int UnreadCount);

/// <summary>Розмова разом зі стрічкою повідомлень, найдавніші зверху.</summary>
public sealed record ConversationDetails(
    long Id,
    long ListingId,
    string ListingTitle,
    string? ListingPhotoPath,
    string CompanionName,

    /// <summary>Чи дивиться на це продавець. Клієнт за цим підписує сторони.</summary>
    bool ViewerIsSeller,
    IReadOnlyList<MessageRecord> Messages);

public sealed record MessageRecord(
    long Id,
    long ConversationId,
    long SenderId,
    string SenderName,
    string Text,
    DateTimeOffset CreatedAt,
    bool IsRead);

/// <summary>Текст повідомлення. Окремий тип, щоб працювала перевірка FluentValidation.</summary>
public sealed record SendMessageRequest
{
    public string Text { get; init; } = string.Empty;
}
