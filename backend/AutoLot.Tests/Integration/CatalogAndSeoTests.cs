using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Xml.Linq;
using AutoLot.Application.Catalog.Validation;

namespace AutoLot.Tests.Integration;

/// <summary>
/// Публічні сторінки, які бачить будь-хто: видача каталогу й два файли для
/// пошукових систем.
///
/// Тут перевіряється те, що починається ще до сервісу, — розбір адресного
/// рядка. Фільтри приїжджають десятками параметрів, серед них перелічення й
/// числа; помилка в прив'язці означає або мовчазно проігнорований фільтр, або
/// п'ятисотку на порожньому місці.
/// </summary>
[Collection(ApiGroup.Name)]
public class CatalogAndSeoTests(ApiFixture api)
{
    [Fact]
    public async Task The_catalogue_answers_with_a_page()
    {
        var page = await api.Client.GetFromJsonAsync<JsonElement>(
            new Uri("/api/catalog", UriKind.Relative));

        Assert.Equal(JsonValueKind.Array, page.GetProperty("items").ValueKind);
        Assert.Equal(1, page.GetProperty("page").GetInt32());
        Assert.True(page.GetProperty("pageSize").GetInt32() > 0);
        Assert.True(page.GetProperty("totalCount").GetInt32() >= 0);
    }

    /// <summary>
    /// Фільтр-перелічення приймається НАЗВОЮ, а не числом: саме так його
    /// віддає довідник, і саме так фронтенд кладе його в адресу.
    /// </summary>
    [Fact]
    public async Task Filters_bind_from_the_address_line()
    {
        using var response = await api.Client.GetAsync(
            new Uri("/api/catalog?Type=Auction&Sort=PriceAscending&Page=1&PageSize=5", UriKind.Relative));

        response.EnsureSuccessStatusCode();

        var page = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(5, page.GetProperty("pageSize").GetInt32());
    }

    /// <summary>
    /// Межа розміру сторінки — не косметика: без неї один запит витягнув би
    /// всю базу. Важливо, що завелике значення саме ВІДХИЛЯЄТЬСЯ, а не тихо
    /// зменшується: мовчазна підміна ховала б помилку в клієнті.
    /// </summary>
    [Fact]
    public async Task An_oversized_page_is_refused()
    {
        var tooMany = CatalogQueryValidator.MaxPageSize + 1;

        using var response = await api.Client.GetAsync(
            new Uri($"/api/catalog?PageSize={tooMany}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_meaningless_filter_value_is_refused()
    {
        using var response = await api.Client.GetAsync(
            new Uri("/api/catalog?Type=Bicycle", UriKind.Relative));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ─────────────────────────── Для пошукових систем ───────────────────────────

    [Fact]
    public async Task Robots_are_told_where_the_sitemap_is()
    {
        using var response = await api.Client.GetAsync(new Uri("/robots.txt", UriKind.Relative));

        response.EnsureSuccessStatusCode();

        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);

        var text = await response.Content.ReadAsStringAsync();

        Assert.Contains("Sitemap:", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// Карта сайту має бути СПРАВЖНІМ xml із простором імен sitemaps.org —
    /// інакше пошуковик її просто не прочитає, і про помилку ніхто не дізнається.
    /// </summary>
    [Fact]
    public async Task The_sitemap_is_valid_xml()
    {
        using var response = await api.Client.GetAsync(new Uri("/sitemap.xml", UriKind.Relative));

        response.EnsureSuccessStatusCode();

        Assert.Equal("application/xml", response.Content.Headers.ContentType?.MediaType);

        var document = XDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(
            XNamespace.Get("http://www.sitemaps.org/schemas/sitemap/0.9") + "urlset",
            document.Root?.Name);
    }
}
