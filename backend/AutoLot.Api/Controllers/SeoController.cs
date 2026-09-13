using System.Globalization;
using System.Text;
using System.Xml.Linq;
using AutoLot.Application.Seo;
using AutoLot.Infrastructure.Email;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AutoLot.Api.Controllers;

/// <summary>
/// Файли для пошукових роботів.
///
/// Обидва віддає API, хоча читають їх як частину сайту. Причина в тому, що
/// карта сайту має містити живі оголошення, а знає про них лише база —
/// статичним файлом у теці фронтенду вона застаріла б наступного дня.
/// </summary>
[ApiController]
[AllowAnonymous]
public sealed class SeoController(
    ISitemapSource sitemap,
    IOptions<EmailOptions> options) : ControllerBase
{
    /// <summary>Адреса сайту — та сама, що в листах і повідомленнях бота.</summary>
    private string SiteUrl => options.Value.SiteUrl.TrimEnd('/');

    [HttpGet("/robots.txt")]
    [Produces("text/plain")]
    public ContentResult Robots()
    {
        var text = new StringBuilder()
            .AppendLine("User-agent: *")
            .AppendLine()

            // Закриваємо те, що не має сенсу в пошуку: особисті розділи й
            // сам API. Не заради таємниці — доступ і так перевіряє сервер, —
            // а щоб робот не витрачав на них обхід.
            .AppendLine("Disallow: /account")
            .AppendLine("Disallow: /chat")
            .AppendLine("Disallow: /favorites")
            .AppendLine("Disallow: /compare")
            .AppendLine("Disallow: /admin")
            .AppendLine("Disallow: /sell")
            .AppendLine("Disallow: /my-dealership")
            .AppendLine("Disallow: /api/")
            .AppendLine()
            .AppendLine(CultureInfo.InvariantCulture, $"Sitemap: {SiteUrl}/sitemap.xml")
            .ToString();

        return Content(text, "text/plain; charset=utf-8");
    }

    [HttpGet("/sitemap.xml")]
    [Produces("application/xml")]
    public async Task<ContentResult> Sitemap(CancellationToken cancellationToken)
    {
        var entries = await sitemap.GetAsync(cancellationToken);

        // Простір імен у карти сайту один і обов'язковий — без нього файл
        // формально не є картою, хоч і виглядає як вона.
        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";

        var document = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement(
                ns + "urlset",
                entries.Select(entry => new XElement(
                    ns + "url",
                    new XElement(ns + "loc", SiteUrl + entry.Path),
                    entry.LastModified is { } modified
                        ? new XElement(ns + "lastmod", modified.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
                        : null,
                    new XElement(
                        ns + "priority",
                        entry.Priority.ToString("0.0", CultureInfo.InvariantCulture))))));

        return Content(document.ToString(), "application/xml; charset=utf-8");
    }
}
