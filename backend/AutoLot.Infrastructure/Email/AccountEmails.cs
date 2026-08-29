using System.Net;
using AutoLot.Application.Common.Abstractions;
using Microsoft.Extensions.Options;

namespace AutoLot.Infrastructure.Email;

/// <summary>
/// Тексти листів про акаунт. Зібрані в одному місці, окремо від відправки:
/// правити формулювання доводиться значно частіше, ніж спосіб доставки.
///
/// Кожен лист має два тіла — HTML і простий текст. Це не примха: лист лише
/// з HTML багато фільтрів вважають за спам, а частина людей читає пошту без
/// картинок і розмітки взагалі.
/// </summary>
internal sealed class AccountEmails(IOptions<EmailOptions> options)
{
    private readonly EmailOptions settings = options.Value;

    public EmailMessage PasswordReset(string to, string token)
    {
        var link = Link("/reset-password", to, token);

        return new EmailMessage(
            to,
            "Відновлення пароля в AutoLot",
            Html(
                "Відновлення пароля",
                "Ви попросили новий пароль до AutoLot. Натисніть кнопку — і зможете задати його.",
                "Задати новий пароль",
                link,
                "Посилання дійсне обмежений час. Якщо ви цього не просили, просто "
                    + "проігноруйте лист: пароль лишиться попереднім."),
            Text(
                "Відновлення пароля в AutoLot",
                "Ви попросили новий пароль. Перейдіть за посиланням, щоб задати його:",
                link,
                "Посилання дійсне обмежений час. Якщо ви цього не просили, "
                    + "проігноруйте лист — пароль лишиться попереднім."));
    }

    public EmailMessage EmailConfirmation(string to, string token)
    {
        var link = Link("/confirm-email", to, token);

        return new EmailMessage(
            to,
            "Підтвердьте пошту в AutoLot",
            Html(
                "Залишився один крок",
                "Підтвердьте, що ця скринька ваша — і ми зможемо надсилати вам "
                    + "сповіщення про ставки й відповіді продавців.",
                "Підтвердити пошту",
                link,
                "Якщо ви не реєструвалися в AutoLot, просто проігноруйте цей лист."),
            Text(
                "Підтвердьте пошту в AutoLot",
                "Підтвердьте, що ця скринька ваша, перейшовши за посиланням:",
                link,
                "Якщо ви не реєструвалися в AutoLot, проігноруйте цей лист."));
    }

    /// <summary>
    /// Найкорисніше сповіщення аукціону: вашу ставку перебили. Без нього
    /// участь у торгах вимагала б сидіти біля екрана сім днів.
    /// </summary>
    public EmailMessage Outbid(string to, string carTitle, string currentPrice, long listingId)
    {
        var link = $"{SiteUrl}/listing/{listingId}";

        return new EmailMessage(
            to,
            $"Вашу ставку перебили: {carTitle}",
            Html(
                "Вашу ставку перебили",
                $"На лоті «{WebUtility.HtmlEncode(carTitle)}» тепер {WebUtility.HtmlEncode(currentPrice)}. "
                    + "Щоб знову вести, підніміть свою стелю.",
                "Перейти до торгів",
                link,
                "Автоставка підніматиме ціну за вас, поки вистачає вашої стелі."),
            Text(
                "Вашу ставку перебили",
                $"На лоті «{carTitle}» тепер {currentPrice}. Щоб знову вести, підніміть свою стелю:",
                link,
                "Автоставка підніматиме ціну за вас, поки вистачає вашої стелі."));
    }

    private string SiteUrl => settings.SiteUrl.TrimEnd('/');

    /// <summary>
    /// Збирає посилання з токеном. Токен обов'язково кодуємо: Identity видає
    /// його у форматі, де трапляються «+» та «/», а в адресі «+» означає
    /// пробіл — без кодування половина посилань не спрацювала б.
    /// </summary>
    private string Link(string path, string email, string token)
    {
        var query = new Dictionary<string, string?>
        {
            ["email"] = email,
            ["token"] = token,
        };

        var parts = query.Select(pair =>
            $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value ?? string.Empty)}");

        return $"{SiteUrl}{path}?{string.Join('&', parts)}";
    }

    /// <summary>
    /// Проста верстка без зовнішніх файлів: усі стилі всередині тегів, бо
    /// поштові програми вирізають майже все інше.
    /// </summary>
    private static string Html(
        string heading,
        string intro,
        string buttonText,
        string link,
        string footer)
    {
        return $"""
            <div style="font-family:-apple-system,Segoe UI,sans-serif;max-width:520px;margin:0 auto;padding:24px;color:#101720">
              <h1 style="font-size:20px;margin:0 0 12px">{WebUtility.HtmlEncode(heading)}</h1>
              <p style="font-size:15px;line-height:1.55;margin:0 0 20px">{intro}</p>
              <p style="margin:0 0 20px">
                <a href="{WebUtility.HtmlEncode(link)}"
                   style="display:inline-block;background:#0E6B74;color:#fff;text-decoration:none;padding:11px 18px;border-radius:6px;font-weight:600">
                  {WebUtility.HtmlEncode(buttonText)}
                </a>
              </p>
              <p style="font-size:13px;line-height:1.5;color:#586574;margin:0">{WebUtility.HtmlEncode(footer)}</p>
            </div>
            """;
    }

    private static string Text(string heading, string intro, string link, string footer)
    {
        return $"""
            {heading}

            {intro}

            {link}

            {footer}
            """;
    }
}
