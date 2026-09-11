using AutoLot.Domain.Common;
using AutoLot.Domain.Identity;

namespace AutoLot.Domain.Bots;

/// <summary>
/// Одноразовий код, яким людина доводить боту, що чат належить саме їй.
///
/// Навіщо код узагалі: бот бачить лише чат у месенджері й не має способу
/// дізнатися, чий це акаунт на майданчику. Питати пошту й пароль у чаті не
/// можна — пароль не передають стороннім застосункам. Тому людина бере код
/// у своєму кабінеті, куди вже увійшла, і надсилає його боту.
///
/// Код не прив'язаний до месенджера: той самий підійде і Telegram, і Viber.
/// Прив'язується він у момент, коли його надсилають.
/// </summary>
public sealed class BotLinkCode : Entity
{
    /// <summary>
    /// Шість цифр. Достатньо коротко, щоб передрукувати з екрана в телефон,
    /// і достатньо, щоб за десять хвилин життя його не вгадали: 900 тисяч
    /// варіантів проти кількох спроб — не та задача, яку розв'язують перебором.
    /// </summary>
    public const int Length = 6;

    /// <summary>Скільки код живе. Довше не треба: його вводять одразу.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    public long UserId { get; set; }

    public User User { get; set; } = null!;

    public string Code { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>
    /// Коли код використали. Не видаляємо рядок одразу: використаний код має
    /// лишатися недійсним до кінця свого строку, інакше той самий шестизначний
    /// набір можна було б застосувати вдруге, щойно його випустять комусь іншому.
    /// </summary>
    public DateTimeOffset? UsedAt { get; set; }

    public bool IsUsable(DateTimeOffset now) => UsedAt is null && ExpiresAt > now;
}
