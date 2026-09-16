using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace AutoLot.Tests.Integration;

/// <summary>
/// Одне авто — одне оголошення в продажу.
///
/// Номер кузова унікальний у світі, тож два оголошення про нього одночасно
/// означають або дубль від самого продавця, або те, чого на майданчику бути не
/// повинно: чужі фото під чужим номером. Ловиться це без жодного зовнішнього
/// сервісу — одним запитом до власної бази.
///
/// Перевіряти доводиться саме звідси: правило живе в сервісі, спирається на
/// стан інших оголошень і спрацьовує на подаванні, а не на створенні.
/// </summary>
[Collection(ApiGroup.Name)]
public class DuplicateVinTests(ApiFixture api)
{
    /// <summary>
    /// Німецький номер, десята позиція якого («M») означає 2021-й — саме той
    /// рік, який ставить DraftAsync.
    /// </summary>
    private const string Vin = "WVWZZZ1JZMW000123";

    [Fact]
    public async Task The_same_car_cannot_be_on_sale_twice()
    {
        var first = await api.SellerAsync();
        var second = await api.SellerAsync();
        var vin = Unique();

        await SubmitAsync(await api.DraftAsync(first, vin), first, HttpStatusCode.NoContent);

        // Другий продавець виставляє те саме авто, поки перше оголошення живе.
        using var refused = await SubmitAsync(
            await api.DraftAsync(second, vin),
            second,
            HttpStatusCode.Conflict);

        var problem = await refused.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Contains("VIN", problem.GetProperty("detail").GetString()!, StringComparison.Ordinal);
    }

    /// <summary>
    /// Той самий продавець теж не виставить одне авто двома оголошеннями —
    /// найчастіший випадок насправді саме цей, а не шахрайство.
    /// </summary>
    [Fact]
    public async Task Not_even_the_same_seller_lists_one_car_twice()
    {
        var seller = await api.SellerAsync();
        var vin = Unique();

        await SubmitAsync(await api.DraftAsync(seller, vin), seller, HttpStatusCode.NoContent);

        (await SubmitAsync(await api.DraftAsync(seller, vin), seller, HttpStatusCode.Conflict)).Dispose();
    }

    /// <summary>
    /// А поки обидва лишаються чернетками, правило мовчить: чернетку ніхто не
    /// бачить, місця у видачі вона не займає, і сваритися ще нема за що.
    /// </summary>
    [Fact]
    public async Task Two_drafts_with_one_vin_bother_nobody()
    {
        var seller = await api.SellerAsync();
        var vin = Unique();

        await api.DraftAsync(seller, vin);
        await api.DraftAsync(seller, vin);
    }

    [Fact]
    public async Task Different_cars_do_not_collide()
    {
        var seller = await api.SellerAsync();

        await SubmitAsync(await api.DraftAsync(seller, Unique()), seller, HttpStatusCode.NoContent);
        await SubmitAsync(await api.DraftAsync(seller, Unique()), seller, HttpStatusCode.NoContent);
    }

    /// <summary>
    /// VIN необов'язковий, і його відсутність не має нікому заважати: інакше
    /// друге оголошення без номера вважалося б дублем першого.
    /// </summary>
    [Fact]
    public async Task Listings_without_a_vin_never_count_as_duplicates()
    {
        var seller = await api.SellerAsync();

        await SubmitAsync(await api.DraftAsync(seller), seller, HttpStatusCode.NoContent);
        await SubmitAsync(await api.DraftAsync(seller), seller, HttpStatusCode.NoContent);
    }

    // ─────────────────────────── Оснащення ───────────────────────────

    /// <summary>
    /// Номер, унікальний для кожного тесту. База спільна на всю групу, тож
    /// сталий VIN у двох тестах зробив би їх залежними один від одного.
    ///
    /// Міняємо лише серійну частину — останні шість символів; позиція року
    /// лишається на місці, інакше валідатор відхилив би номер.
    /// </summary>
    private static string Unique() =>
        string.Concat(
            Vin.AsSpan(0, 11),
            Random.Shared.Next(100_000, 999_999).ToString(CultureInfo.InvariantCulture));

    private async Task<HttpResponseMessage> SubmitAsync(
        long listingId,
        string token,
        HttpStatusCode expected)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri($"/api/listings/{listingId}/submit", UriKind.Relative));

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await api.Client.SendAsync(request);

        Assert.Equal(expected, response.StatusCode);

        return response;
    }
}
