using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace AutoLot.Tests.Integration;

/// <summary>
/// Найпростіші звернення до застосунку — і водночас найважливіші: якщо вони
/// не проходять, решта перевірок нічого не варта.
///
/// Заразом це єдине місце, де перевіряється, що застосунок узагалі
/// піднімається: конвеєр зібраний, залежності в контейнері знайшлися, довідники
/// засіялися. Модульні тести про це не знають нічого.
/// </summary>
[Collection(ApiGroup.Name)]
public class HealthAndReferenceTests(ApiFixture api)
{
    [Fact]
    public async Task The_application_is_alive()
    {
        var response = await api.Client.GetAsync(new Uri("/health/live", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// «Готовий» — це вже не про процес, а про базу: перевірка ходить у
    /// PostgreSQL. Якщо тимчасова схема не створилася, видно буде тут.
    /// </summary>
    [Fact]
    public async Task The_application_reaches_its_database()
    {
        var response = await api.Client.GetAsync(new Uri("/health/ready", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Довідники засіваються при старті. Якщо сід мовчки впав — а він ловить
    /// помилки бази й лише пише в лог, — список приїхав би порожнім.
    /// </summary>
    [Fact]
    public async Task The_geography_reference_is_seeded()
    {
        var regions = await api.Client.GetFromJsonAsync<JsonElement>(
            new Uri("/api/geo/regions", UriKind.Relative));

        Assert.Equal(JsonValueKind.Array, regions.ValueKind);

        // Двадцять сім областей, АР Крим і два міста зі спеціальним статусом.
        Assert.True(regions.GetArrayLength() >= 25, "областей має бути не менше двадцяти п'яти");
    }

    [Fact]
    public async Task The_car_reference_is_seeded()
    {
        var makes = await api.Client.GetFromJsonAsync<JsonElement>(
            new Uri("/api/cars/makes", UriKind.Relative));

        Assert.True(makes.GetArrayLength() >= 40, "марок має бути не менше сорока");
    }

    /// <summary>
    /// Назви довідників сервер віддає мовою із заголовка. Перевірити це без
    /// HTTP неможливо: вибір мови робить middleware, а не сервіс.
    /// </summary>
    [Fact]
    public async Task Reference_names_follow_the_requested_language()
    {
        var ukrainian = await AttributeNameAsync("uk");
        var english = await AttributeNameAsync("en");

        Assert.NotEqual(ukrainian, english);
    }

    /// <summary>
    /// Невідома мова не має бути помилкою: беремо українську й працюємо далі.
    /// </summary>
    [Fact]
    public async Task An_unknown_language_falls_back_to_ukrainian()
    {
        Assert.Equal(await AttributeNameAsync("uk"), await AttributeNameAsync("de-DE"));
    }

    /// <summary>Перша назва кузова — рядок, якого вистачає, щоб побачити мову.</summary>
    private async Task<string?> AttributeNameAsync(string language)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            new Uri("/api/cars/attributes", UriKind.Relative));

        request.Headers.Add("Accept-Language", language);

        using var response = await api.Client.SendAsync(request);

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        return payload.GetProperty("bodyTypes")[0].GetProperty("name").GetString();
    }
}
