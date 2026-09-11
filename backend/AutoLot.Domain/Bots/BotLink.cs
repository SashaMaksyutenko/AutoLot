using AutoLot.Domain.Common;
using AutoLot.Domain.Identity;

namespace AutoLot.Domain.Bots;

/// <summary>Месенджер, у якому живе бот.</summary>
public enum BotProvider
{
    Telegram = 0,
    Viber = 1,
}

/// <summary>
/// Зв'язок акаунта на майданчику з чатом у месенджері.
///
/// Окремою сутністю, а не полем у профілі: месенджерів обіцяно два, і в
/// кожного свій чат. Поля <c>TelegramChatId</c> і <c>ViberChatId</c> поруч у
/// користувачі довелося б додавати щоразу заново.
/// </summary>
public sealed class BotLink : Entity
{
    public long UserId { get; set; }

    public User User { get; set; } = null!;

    public BotProvider Provider { get; set; }

    /// <summary>
    /// Ідентифікатор чату. Рядок, а не число: у Telegram це число, а у Viber —
    /// рядок із літер, і зводити їх до спільного типу довелося б однаково.
    /// </summary>
    public string ChatId { get; set; } = string.Empty;

    /// <summary>Як до людини звертатися в месенджері. Лише для привітання.</summary>
    public string? ChatName { get; set; }

    public DateTimeOffset LinkedAt { get; set; }
}
