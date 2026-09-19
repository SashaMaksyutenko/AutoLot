using System.Net.Http.Json;
using System.Text.Json;

namespace AutoLot.Tests.Integration;

/// <summary>
/// Схожі авто під карткою.
///
/// Перевіряти доводиться звідси, а не модульно: і відбір, і сортування виконує
/// сама база — «спершу та сама модель», «ближча ціна першою» перекладаються в
/// SQL, і помилка в такому перекладі модульного тесту не зачепила б.
///
/// Кожен тест працює у своєму ціновому поясі (10, 40, 160, 640 тисяч): вікно
/// схожості — чверть ціни в обидва боки, тож пояси не перетинаються, і
/// оголошення одного тесту не потрапляють у вибірку іншого.
/// </summary>
[Collection(ApiGroup.Name)]
public class SimilarListingsTests(ApiFixture api)
{
    /// <summary>
    /// Та сама модель іде першою, навіть якщо інша модель тієї ж марки
    /// ближча за ціною: людина, що дивиться на конкретну модель, насамперед
    /// хоче бачити саме її.
    /// </summary>
    [Fact]
    public async Task The_same_model_comes_first_then_the_closest_price()
    {
        var seller = await api.SellerAsync();
        var (make, modelA, modelB) = await api.SpareMakeAsync();

        var source = await PublishedAsync(seller, (make, modelA), 40_000);
        var sameModelNear = await PublishedAsync(seller, (make, modelA), 41_000);
        var sameModelFar = await PublishedAsync(seller, (make, modelA), 45_000);
        var otherModelNearest = await PublishedAsync(seller, (make, modelB), 40_500);

        var similar = await SimilarAsync(source);

        Assert.Equal([sameModelNear, sameModelFar, otherModelNearest], similar);
    }

    /// <summary>
    /// Авто вдвічі дорожче — не альтернатива, а інша полиця. Вікно — чверть
    /// ціни в обидва боки.
    /// </summary>
    [Fact]
    public async Task A_car_far_outside_the_budget_is_left_out()
    {
        var seller = await api.SellerAsync();
        var (make, model, _) = await api.SpareMakeAsync();

        var source = await PublishedAsync(seller, (make, model), 160_000);
        var inside = await PublishedAsync(seller, (make, model), 180_000);
        await PublishedAsync(seller, (make, model), 250_000);

        Assert.Equal([inside], await SimilarAsync(source));
    }

    /// <summary>
    /// Саме оголошення серед схожих на себе не показуємо, чернеток — теж:
    /// їх ніхто, крім автора, не бачить.
    /// </summary>
    [Fact]
    public async Task Neither_the_listing_itself_nor_drafts_are_offered()
    {
        var seller = await api.SellerAsync();
        var (make, model, _) = await api.SpareMakeAsync();

        var source = await PublishedAsync(seller, (make, model), 640_000);
        await api.DraftAsync(seller, make: (make, model), price: 640_000);

        Assert.Empty(await SimilarAsync(source));
    }

    /// <summary>
    /// Схожі на чужу чернетку видали б, що це за авто й скільки воно коштує, —
    /// тобто рівно те, що чернетка приховує.
    /// </summary>
    [Fact]
    public async Task A_strangers_draft_has_no_similar_listings()
    {
        var seller = await api.SellerAsync();
        var (make, model, _) = await api.SpareMakeAsync();

        await PublishedAsync(seller, (make, model), 10_000);
        var draft = await api.DraftAsync(seller, make: (make, model), price: 10_000);

        Assert.Empty(await SimilarAsync(draft));
    }

    // ─────────────────────────── Оснащення ───────────────────────────

    private async Task<long> PublishedAsync(string token, (long MakeId, long ModelId) make, decimal price)
    {
        var listingId = await api.DraftAsync(token, make: make, price: price);

        await api.PublishAsync(listingId, token);

        return listingId;
    }

    private async Task<long[]> SimilarAsync(long listingId)
    {
        var listings = await api.Client.GetFromJsonAsync<JsonElement>(
            new Uri($"/api/listings/{listingId}/similar", UriKind.Relative));

        return [.. listings.EnumerateArray().Select(listing => listing.GetProperty("id").GetInt64())];
    }
}
