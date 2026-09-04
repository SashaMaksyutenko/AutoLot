using System.Globalization;
using System.Net;
using AutoLot.Application.Common.Abstractions;
using AutoLot.Application.Listings.Dtos;
using Microsoft.Extensions.Options;

namespace AutoLot.Infrastructure.Email;

/// <summary>
/// Лист про нові збіги в збереженому пошуку.
///
/// Окремо від <see cref="AccountEmails"/>: там листи про сам акаунт —
/// пароль, підтвердження пошти. Тут інша тема й інший привід, і змішувати
/// їх в одному класі означало б робити його «файлом усіх листів».
/// </summary>
internal sealed class SearchEmails(IOptions<EmailOptions> options)
{
    private readonly EmailOptions settings = options.Value;

    public EmailMessage NewMatches(
        string to,
        string searchName,
        long searchId,
        IReadOnlyList<ListingSummary> listings,
        int totalCount)
    {
        ArgumentNullException.ThrowIfNull(listings);

        var link = $"{SiteUrl}/?savedSearch={searchId}";
        var word = Plural(totalCount, "нове авто", "нові авто", "нових авто");

        // Тема називає і кількість, і сам пошук: людина може мати їх кілька,
        // і «нові оголошення» в скриньці нічого б їй не сказало.
        var subject = $"{totalCount} {word} за пошуком «{searchName}»";

        var rest = totalCount > listings.Count
            ? $"…і ще {totalCount - listings.Count}. "
            : string.Empty;

        return new EmailMessage(
            to,
            subject,
            Html(searchName, listings, rest, link),
            Text(searchName, listings, rest, link));
    }

    private string SiteUrl => settings.SiteUrl.TrimEnd('/');

    private string Html(
        string searchName,
        IReadOnlyList<ListingSummary> listings,
        string rest,
        string link)
    {
        // HtmlEncode обов'язковий скрізь, де в розмітку потрапляє чуже:
        // назву пошуку писала людина, а заголовок оголошення — продавець.
        var rows = string.Join(
            "\n",
            listings.Select(listing =>
                $"""
                <tr>
                  <td style="padding:8px 0;border-bottom:1px solid #eee">
                    <a href="{SiteUrl}/listing/{listing.Id}" style="color:#111;text-decoration:none">
                      <strong>{WebUtility.HtmlEncode(TitleOf(listing))}</strong>
                    </a><br>
                    <span style="color:#666;font-size:13px">{WebUtility.HtmlEncode(DetailsOf(listing))}</span>
                  </td>
                </tr>
                """));

        return $"""
            <!doctype html>
            <html lang="uk">
              <body style="font-family:system-ui,sans-serif;color:#111;line-height:1.5">
                <h2 style="margin:0 0 4px">За пошуком «{WebUtility.HtmlEncode(searchName)}»</h2>
                <p style="margin:0 0 16px;color:#666">З'явилося нове. Ось найсвіжіше:</p>
                <table style="width:100%;border-collapse:collapse">{rows}</table>
                <p style="margin:16px 0">
                  {WebUtility.HtmlEncode(rest)}<a href="{link}">Подивитися всі</a>
                </p>
                <p style="color:#888;font-size:12px">
                  Сповіщення можна вимкнути в списку збережених пошуків.
                </p>
              </body>
            </html>
            """;
    }

    private string Text(
        string searchName,
        IReadOnlyList<ListingSummary> listings,
        string rest,
        string link)
    {
        var lines = string.Join(
            "\n",
            listings.Select(listing =>
                $"— {TitleOf(listing)}: {DetailsOf(listing)}\n  {SiteUrl}/listing/{listing.Id}"));

        return $"""
            За пошуком «{searchName}» з'явилося нове.

            {lines}

            {rest}Подивитися всі: {link}

            Сповіщення можна вимкнути в списку збережених пошуків.
            """;
    }

    private static string TitleOf(ListingSummary listing) =>
        $"{listing.Make} {listing.Model} {listing.Year}";

    private static string DetailsOf(ListingSummary listing)
    {
        var price = listing.Price.ToString("N0", CultureInfo.GetCultureInfo("uk-UA"));
        var parts = new List<string> { $"{price} {Sign(listing.Currency)}" };

        if (listing.Mileage is { } mileage)
        {
            parts.Add($"{mileage.ToString("N0", CultureInfo.GetCultureInfo("uk-UA"))} км");
        }

        parts.Add(listing.CityName);

        return string.Join(" · ", parts);
    }

    private static string Sign(Domain.Enums.Currency currency) => currency switch
    {
        Domain.Enums.Currency.Usd => "$",
        Domain.Enums.Currency.Eur => "€",
        _ => "₴",
    };

    /// <summary>
    /// Українська форма множини. Та сама, що на клієнті: «1 нове авто»,
    /// «2 нові авто», «5 нових авто» — інакше тема листа читалася б як
    /// машинний переклад.
    /// </summary>
    private static string Plural(int count, string one, string few, string many)
    {
        var mod100 = count % 100;

        if (mod100 is >= 11 and <= 14)
        {
            return many;
        }

        return (count % 10) switch
        {
            1 => one,
            2 or 3 or 4 => few,
            _ => many,
        };
    }
}
