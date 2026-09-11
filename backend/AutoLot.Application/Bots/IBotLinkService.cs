using AutoLot.Domain.Bots;

namespace AutoLot.Application.Bots;

/// <summary>Код для прив'язки, яким людина ділиться з ботом.</summary>
public sealed record BotLinkCodeIssued(string Code, DateTimeOffset ExpiresAt);

/// <summary>Куди й кому бот може писати.</summary>
public sealed record BotRecipient(long UserId, BotProvider Provider, string ChatId);

/// <summary>Чим закінчилася спроба прив'язати чат.</summary>
public enum BotLinkOutcome
{
    /// <summary>Прив'язали вперше.</summary>
    Linked = 0,

    /// <summary>Цей чат уже належав цьому ж акаунту — нічого не змінилося.</summary>
    AlreadyLinked,

    /// <summary>Коду немає, він протермінований або вже використаний.</summary>
    CodeRejected,
}

/// <summary>
/// Прив'язка акаунта до чату в месенджері.
///
/// Служба навмисно нічого не знає про конкретний месенджер: вона оперує
/// парою «провайдер + ідентифікатор чату». Через це той самий код обслуговує
/// і Telegram, і Viber, а різниця між ними лишається там, де їй і місце — у
/// клієнті месенджера.
/// </summary>
public interface IBotLinkService
{
    /// <summary>
    /// Видає код у кабінеті. Попередні невикористані коди цієї людини
    /// гасяться: діяти має лише той, що зараз на екрані.
    /// </summary>
    Task<BotLinkCodeIssued> IssueCodeAsync(long userId, CancellationToken cancellationToken = default);

    /// <summary>Приймає код від бота й пов'язує чат з акаунтом.</summary>
    Task<BotLinkOutcome> RedeemAsync(
        BotProvider provider,
        string chatId,
        string? chatName,
        string code,
        CancellationToken cancellationToken = default);

    /// <summary>Кому належить цей чат. <c>null</c> — чат ще не прив'язаний.</summary>
    Task<long?> FindUserAsync(
        BotProvider provider,
        string chatId,
        CancellationToken cancellationToken = default);

    /// <summary>Відв'язати чат. Команда «зупинити» в самому боті.</summary>
    Task<bool> UnlinkAsync(
        BotProvider provider,
        string chatId,
        CancellationToken cancellationToken = default);

    /// <summary>Куди писати цьому користувачеві. Потрібно для сповіщень.</summary>
    Task<IReadOnlyList<BotRecipient>> GetRecipientsAsync(
        long userId,
        CancellationToken cancellationToken = default);
}
