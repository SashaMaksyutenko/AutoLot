using AutoLot.Application.Bots;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutoLot.Infrastructure.Bots;

/// <summary>
/// Ім'я бота питаємо в Telegram один раз і запам'ятовуємо.
///
/// Змінитися воно може лише в @BotFather, тобто раз на ніколи, — а кабінет
/// відкривають часто. Ходити по мережу на кожне відкриття сторінки заради
/// незмінного рядка немає сенсу.
/// </summary>
internal sealed partial class BotDirectory(
    TelegramClient telegram,
    IOptions<TelegramOptions> options,
    ILogger<BotDirectory> logger) : IBotDirectory
{
    private readonly TelegramOptions settings = options.Value;

    private TelegramBotInfo? cached;

    public async Task<TelegramBotInfo> GetTelegramAsync(CancellationToken cancellationToken = default)
    {
        if (!settings.IsConfigured)
        {
            return new TelegramBotInfo(false, null);
        }

        if (cached is not null)
        {
            return cached;
        }

        try
        {
            var username = await telegram.GetUsernameAsync(cancellationToken);

            // Запам'ятовуємо лише успіх: інакше одна невдала хвилина мережі
            // назавжди лишила б кабінет без посилання на бота.
            cached = new TelegramBotInfo(true, username);

            return cached;
        }
        catch (Exception exception)
        {
            LogLookupFailed(logger, exception);

            // Бот налаштований, просто зараз недосяжний. Кажемо «увімкнений,
            // імені не знаю»: картка покаже код і пояснить, як знайти бота
            // вручну, замість того щоб зникнути.
            return new TelegramBotInfo(true, null);
        }
    }

    [LoggerMessage(
        EventId = 253,
        Level = LogLevel.Warning,
        Message = "Не вдалося дізнатися ім'я телеграм-бота")]
    private static partial void LogLookupFailed(ILogger logger, Exception exception);
}
