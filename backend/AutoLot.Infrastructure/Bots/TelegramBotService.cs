using AutoLot.Application.Bots;
using AutoLot.Domain.Bots;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutoLot.Infrastructure.Bots;

/// <summary>
/// Телеграм-бот: слухає повідомлення й відповідає на команди.
///
/// Працює довгим опитуванням, а не вебхуками. Вебхук вимагає публічної
/// HTTPS-адреси, тобто розробляти бота на власному комп'ютері було б
/// неможливо без тунелю. Опитування ж однаково працює і з localhost, і з
/// хостингу — ціна цього лише в тому, що один запит завжди «висить».
/// </summary>
internal sealed partial class TelegramBotService(
    TelegramClient telegram,
    IServiceScopeFactory scopes,
    IOptions<TelegramOptions> options,
    ILogger<TelegramBotService> logger) : BackgroundService
{
    private readonly TelegramOptions settings = options.Value;

    /// <summary>
    /// Номер останнього обробленого оновлення. Тримаємо в пам'яті: якщо
    /// застосунок перезапустять, Telegram просто віддасть кілька останніх
    /// повідомлень удруге, а повторна команда нічого не псує.
    /// </summary>
    private long offset;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!settings.IsConfigured)
        {
            LogNotConfigured(logger);

            return;
        }

        LogStarted(logger);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var messages = await telegram.GetUpdatesAsync(offset, stoppingToken);

                foreach (var message in messages)
                {
                    // Зсув посуваємо ДО обробки: повідомлення, на якому
                    // обробник спіткнувся, не має повертатися по колу й
                    // зупиняти всю чергу.
                    offset = message.UpdateId + 1;

                    await HandleAsync(message, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Застосунок зупиняють — це не збій.
                break;
            }
            catch (Exception exception)
            {
                LogPollFailed(logger, exception);

                await Task.Delay(
                    TimeSpan.FromSeconds(settings.RetryDelaySeconds),
                    stoppingToken);
            }
        }
    }

    private async Task HandleAsync(TelegramMessage message, CancellationToken cancellationToken)
    {
        // Своя область на кожне повідомлення: служби роботи з базою живуть
        // у межах запиту, а фонова служба — синглтон і власної області не має.
        await using var scope = scopes.CreateAsyncScope();

        var links = scope.ServiceProvider.GetRequiredService<IBotLinkService>();

        var text = message.Text.Trim();
        var command = text.Split(' ', 2)[0].ToLowerInvariant();
        var argument = text.Contains(' ', StringComparison.Ordinal)
            ? text.Split(' ', 2)[1].Trim()
            : string.Empty;

        var reply = command switch
        {
            "/start" or "/help" => await GreetingAsync(links, message, cancellationToken),
            "/link" => await LinkAsync(links, message, argument, cancellationToken),
            "/stop" => await UnlinkAsync(links, message, cancellationToken),
            _ => await UnknownAsync(links, message, text, cancellationToken),
        };

        await telegram.SendAsync(message.ChatId, reply, cancellationToken);
    }

    private static async Task<string> GreetingAsync(
        IBotLinkService links,
        TelegramMessage message,
        CancellationToken cancellationToken)
    {
        var linked = await links.FindUserAsync(BotProvider.Telegram, message.ChatId, cancellationToken);

        var name = string.IsNullOrWhiteSpace(message.ChatName) ? string.Empty : $", {message.ChatName}";

        return linked is null
            ? $"Вітаю{name}! Я бот AutoLot.\n\n"
              + "Щоб я знав, чий ви акаунт, візьміть код у кабінеті на сайті "
              + "(розділ «Телеграм») і надішліть його сюди так:\n\n"
              + "/link 123456\n\n"
              + "Після цього я надсилатиму сповіщення про нові авто за вашими "
              + "збереженими пошуками й про перебиті ставки."
            : $"Вітаю{name}! Акаунт уже прив'язаний.\n\n"
              + "/stop — відв'язати цей чат.";
    }

    private static async Task<string> LinkAsync(
        IBotLinkService links,
        TelegramMessage message,
        string code,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return "Після команди потрібен код із кабінету, наприклад:\n/link 123456";
        }

        var outcome = await links.RedeemAsync(
            BotProvider.Telegram,
            message.ChatId,
            message.ChatName,
            code,
            cancellationToken);

        return outcome switch
        {
            BotLinkOutcome.Linked => "Готово — акаунт прив'язано. Тепер я писатиму вам про нові авто за збереженими пошуками й про перебиті ставки.",
            BotLinkOutcome.AlreadyLinked => "Цей чат уже прив'язаний до того самого акаунта.",
            _ => "Код не підійшов: він діє десять хвилин і лише один раз. Візьміть новий у кабінеті.",
        };
    }

    private static async Task<string> UnlinkAsync(
        IBotLinkService links,
        TelegramMessage message,
        CancellationToken cancellationToken)
    {
        var removed = await links.UnlinkAsync(BotProvider.Telegram, message.ChatId, cancellationToken);

        return removed
            ? "Чат відв'язано. Сповіщень більше не буде. Щоб повернути — /link із новим кодом."
            : "Цей чат і не був прив'язаний.";
    }

    private static async Task<string> UnknownAsync(
        IBotLinkService links,
        TelegramMessage message,
        string text,
        CancellationToken cancellationToken)
    {
        // Людина могла надіслати самий код, без команди — це найчастіша
        // помилка, і відповідати на неї «не розумію» було б недоречно.
        if (text.Length == BotLinkCode.Length && text.All(char.IsAsciiDigit))
        {
            return await LinkAsync(links, message, text, cancellationToken);
        }

        return "Не знаю такої команди.\n\n"
            + "/link 123456 — прив'язати акаунт кодом із кабінету\n"
            + "/stop — відв'язати цей чат\n"
            + "/help — це повідомлення";
    }

    // ── Журнал ───────────────────────────────────────────────────────

    [LoggerMessage(
        EventId = 250,
        Level = LogLevel.Information,
        Message = "Телеграм-бот не налаштований — токена немає, слухати не будемо")]
    private static partial void LogNotConfigured(ILogger logger);

    [LoggerMessage(EventId = 251, Level = LogLevel.Information, Message = "Телеграм-бот слухає повідомлення")]
    private static partial void LogStarted(ILogger logger);

    [LoggerMessage(
        EventId = 252,
        Level = LogLevel.Warning,
        Message = "Не вдалося отримати оновлення Telegram; спробуємо ще раз")]
    private static partial void LogPollFailed(ILogger logger, Exception exception);
}
