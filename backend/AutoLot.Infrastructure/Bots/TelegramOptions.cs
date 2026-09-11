namespace AutoLot.Infrastructure.Bots;

/// <summary>
/// Налаштування телеграм-бота. Токен видає @BotFather і зберігається він
/// лише в user-secrets — у репозиторії його немає (SPEC §8).
/// </summary>
public sealed class TelegramOptions
{
    public const string SectionName = "Bots:Telegram";

    /// <summary>Порожній токен означає, що бота просто не піднімають.</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Скільки секунд сервер Telegram тримає запит, якщо новин немає.
    ///
    /// Це і є «довге опитування»: замість того щоб питати щосекунди й
    /// отримувати порожнечу, один запит чекає на сервері до півхвилини й
    /// повертається щойно щось з'явиться. Менше запитів і швидша реакція
    /// водночас.
    /// </summary>
    public int PollTimeoutSeconds { get; set; } = 25;

    /// <summary>Скільки чекати після збою, перш ніж пробувати знову.</summary>
    public int RetryDelaySeconds { get; set; } = 10;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Token);
}
