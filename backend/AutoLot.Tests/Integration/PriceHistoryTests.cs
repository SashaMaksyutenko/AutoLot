using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;

namespace AutoLot.Tests.Integration;

/// <summary>
/// Зміна ціни та її історія.
///
/// Тут перевіряється рішення, без якого весь пункт не мав би сенсу: ціну
/// опублікованого оголошення МОЖНА міняти, хоча решту — ні. Опубліковане
/// редагувати не можна взагалі, інакше після модерації в ньому підмінили б
/// і фото, і опис. А ціна рухається постійно — саме зниження ціни й продає
/// авто, і ганяти оголошення через чергу щоразу було б безглуздо.
/// </summary>
[Collection(ApiGroup.Name)]
public class PriceHistoryTests(ApiFixture api)
{
    [Fact]
    public async Task A_published_listing_starts_with_one_point()
    {
        var seller = await api.SellerAsync();
        var listingId = await api.DraftAsync(seller);

        await api.PublishAsync(listingId, seller);

        // Перша точка — ціна, з якою оголошення виставили. Без неї графік
        // починався б із другої ціни, і перше зниження виглядало б так,
        // ніби авто одразу подали дешевшим.
        Assert.Equal(1, (await HistoryAsync(listingId)).GetArrayLength());
    }

    [Fact]
    public async Task Lowering_the_price_adds_a_point()
    {
        var seller = await api.SellerAsync();
        var listingId = await api.DraftAsync(seller);

        await api.PublishAsync(listingId, seller);
        await ChangePriceAsync(listingId, seller, 22_000, HttpStatusCode.NoContent);

        var history = await HistoryAsync(listingId);

        Assert.Equal(2, history.GetArrayLength());
        Assert.Equal(25_000, history[0].GetProperty("price").GetDecimal());
        Assert.Equal(22_000, history[1].GetProperty("price").GetDecimal());
    }

    /// <summary>
    /// Оголошення, виставлене ще ДО появи історії цін, не має жодної точки.
    /// Перша ж зміна має дати графік із двох — старої ціни й нової. Інакше
    /// продавцеві довелося б змінювати ціну двічі, щоб покупці побачили
    /// перше зниження.
    /// </summary>
    [Fact]
    public async Task An_older_listing_gets_its_starting_price_on_the_first_change()
    {
        var seller = await api.SellerAsync();
        var listingId = await api.DraftAsync(seller);

        await api.PublishAsync(listingId, seller);

        // Стираємо історію — так виглядає оголошення, створене раніше.
        await ForgetHistoryAsync(listingId);

        await ChangePriceAsync(listingId, seller, 21_000, HttpStatusCode.NoContent);

        var history = await HistoryAsync(listingId);

        Assert.Equal(2, history.GetArrayLength());
        Assert.Equal(25_000, history[0].GetProperty("price").GetDecimal());
        Assert.Equal(21_000, history[1].GetProperty("price").GetDecimal());
    }

    /// <summary>
    /// Та сама ціна вдруге історію не засмічує: інакше кожне натискання
    /// «Зберегти» додавало б у графік точку, яка нічого не означає.
    /// </summary>
    [Fact]
    public async Task Saving_the_same_price_changes_nothing()
    {
        var seller = await api.SellerAsync();
        var listingId = await api.DraftAsync(seller);

        await api.PublishAsync(listingId, seller);
        await ChangePriceAsync(listingId, seller, 25_000, HttpStatusCode.NoContent);

        Assert.Equal(1, (await HistoryAsync(listingId)).GetArrayLength());
    }

    /// <summary>
    /// У чернетці ціну міняють звичайним редагуванням — окремої дії для неї
    /// немає, і вона має чесно про це сказати.
    /// </summary>
    [Fact]
    public async Task A_draft_has_no_separate_price_action()
    {
        var seller = await api.SellerAsync();
        var listingId = await api.DraftAsync(seller);

        await ChangePriceAsync(listingId, seller, 20_000, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task A_stranger_cannot_reprice_someone_elses_car()
    {
        var seller = await api.SellerAsync();
        var stranger = await api.SellerAsync();
        var listingId = await api.DraftAsync(seller);

        await api.PublishAsync(listingId, seller);
        await ChangePriceAsync(listingId, stranger, 1_000, HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5000)]
    public async Task A_meaningless_price_is_refused(decimal price)
    {
        var seller = await api.SellerAsync();
        var listingId = await api.DraftAsync(seller);

        await api.PublishAsync(listingId, seller);
        await ChangePriceAsync(listingId, seller, price, HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Історію опублікованого оголошення бачать усі — у цьому весь сенс.
    /// Гість, який зайшов з пошуку, має бачити те саме, що й покупець із
    /// акаунтом.
    /// </summary>
    [Fact]
    public async Task A_guest_sees_the_history_of_a_published_listing()
    {
        var seller = await api.SellerAsync();
        var listingId = await api.DraftAsync(seller);

        await api.PublishAsync(listingId, seller);
        await ChangePriceAsync(listingId, seller, 23_500, HttpStatusCode.NoContent);

        var history = await api.Client.GetFromJsonAsync<JsonElement>(
            new Uri($"/api/listings/{listingId}/price-history", UriKind.Relative));

        Assert.Equal(2, history.GetArrayLength());
    }

    /// <summary>
    /// А історія чужої чернетки — така сама таємниця, як і сама чернетка.
    /// Порожній перелік, а не відмова: це доповнення до картки, і окрема
    /// помилка тут нічого корисного не додала б.
    /// </summary>
    [Fact]
    public async Task The_history_of_a_strangers_draft_is_empty()
    {
        var seller = await api.SellerAsync();
        var listingId = await api.DraftAsync(seller);

        var history = await api.Client.GetFromJsonAsync<JsonElement>(
            new Uri($"/api/listings/{listingId}/price-history", UriKind.Relative));

        Assert.Equal(0, history.GetArrayLength());
    }

    // ─────────────────────────── Оснащення ───────────────────────────

    private async Task ChangePriceAsync(
        long listingId,
        string token,
        decimal price,
        HttpStatusCode expected)
    {
        using var response = await api.SendAsync(
            HttpMethod.Put,
            $"/api/listings/{listingId}/price",
            token,
            new { price, currency = "Usd" });

        Assert.Equal(expected, response.StatusCode);
    }

    /// <summary>
    /// Прибирає історію оголошення прямо в базі — через HTTP так зробити не
    /// можна, і це правильно: записи історії не видаляються ніким.
    /// </summary>
    private async Task ForgetHistoryAsync(long listingId)
    {
        await using var connection = new NpgsqlConnection(api.ConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            "DELETE FROM price_changes WHERE listing_id = @listing",
            connection);

        command.Parameters.AddWithValue("listing", listingId);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<JsonElement> HistoryAsync(long listingId) =>
        await api.Client.GetFromJsonAsync<JsonElement>(
            new Uri($"/api/listings/{listingId}/price-history", UriKind.Relative));
}
