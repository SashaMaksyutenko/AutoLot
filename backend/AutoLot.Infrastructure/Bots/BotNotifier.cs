using System.Globalization;
using AutoLot.Application.Bots;
using AutoLot.Application.Listings.Dtos;
using AutoLot.Domain.Bots;
using AutoLot.Infrastructure.Email;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutoLot.Infrastructure.Bots;

/// <summary>
/// Надсилає сповіщення в усі прив'язані месенджери.
///
/// Скільки авто показувати в одному повідомленні: три. Лист собі може
/// дозволити довгий список — його читають з екрана комп'ютера і його можна
/// прогорнути. Повідомлення ж приходить на телефон і має вміститися в
/// сповіщення на замкненому екрані; решту людина побачить за посиланням.
/// </summary>
internal sealed partial class BotNotifier(
    IBotLinkService links,
    TelegramClient telegram,
    IOptions<TelegramOptions> telegramOptions,
    IOptions<EmailOptions> emailOptions,
    ILogger<BotNotifier> logger) : IBotNotifier
{
    private const int PerMessage = 3;

    private readonly TelegramOptions telegramSettings = telegramOptions.Value;

    /// <summary>Адресу сайту беремо ту саму, що й листи: вона в проєкті одна.</summary>
    private string SiteUrl => emailOptions.Value.SiteUrl.TrimEnd('/');

    public async Task<int> NotifyNewMatchesAsync(
        long userId,
        string searchName,
        IReadOnlyList<ListingSummary> found,
        int total,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(found);

        var lines = new List<string>
        {
            $"За пошуком «{searchName}» з'явилося нове: {total}.",
            string.Empty,
        };

        foreach (var listing in found.Take(PerMessage))
        {
            lines.Add($"{listing.Make} {listing.Model} {listing.Year} — {Money(listing.Price, listing.Currency)}");
            lines.Add($"{SiteUrl}/listing/{listing.Id}");
            lines.Add(string.Empty);
        }

        if (total > PerMessage)
        {
            lines.Add($"Решта — на сайті: {SiteUrl}/");
        }

        return await SendAsync(userId, string.Join('\n', lines).TrimEnd(), cancellationToken);
    }

    public async Task NotifyOutbidAsync(
        long userId,
        string listingTitle,
        string price,
        long listingId,
        CancellationToken cancellationToken = default)
    {
        var text =
            $"Вашу ставку перебили.\n\n{listingTitle}\nПоточна ціна: {price}\n\n"
            + $"{SiteUrl}/listing/{listingId}";

        await SendAsync(userId, text, cancellationToken);
    }

    /// <summary>
    /// Розсилає текст у всі чати людини.
    ///
    /// Помилка одного чату не спиняє решти й не виходить назовні: цей виклик
    /// стоїть усередині чужої дії — розсилки або ставки, — і недоступний
    /// Telegram не має її зривати.
    /// </summary>
    private async Task<int> SendAsync(long userId, string text, CancellationToken cancellationToken)
    {
        IReadOnlyList<BotRecipient> recipients;

        try
        {
            recipients = await links.GetRecipientsAsync(userId, cancellationToken);
        }
        catch (Exception exception)
        {
            LogFailed(logger, userId, exception);

            return 0;
        }

        var reached = 0;

        foreach (var recipient in recipients)
        {
            // Поки що ходить лише Telegram. Viber з'явиться тут же — саме
            // тому одержувач і несе в собі провайдера.
            if (recipient.Provider != BotProvider.Telegram || !telegramSettings.IsConfigured)
            {
                continue;
            }

            try
            {
                await telegram.SendAsync(recipient.ChatId, text, cancellationToken);
                reached++;
            }
            catch (Exception exception)
            {
                LogFailed(logger, userId, exception);
            }
        }

        return reached;
    }

    private static string Money(decimal amount, Domain.Enums.Currency currency) =>
        string.Create(CultureInfo.InvariantCulture, $"{amount:0.##} {currency}");

    [LoggerMessage(
        EventId = 254,
        Level = LogLevel.Warning,
        Message = "Не вдалося надіслати сповіщення в месенджер користувачу {UserId}")]
    private static partial void LogFailed(ILogger logger, long userId, Exception exception);
}
