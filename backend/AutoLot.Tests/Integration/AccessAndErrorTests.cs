using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace AutoLot.Tests.Integration;

/// <summary>
/// Хто куди не має доступу і як виглядають помилки.
///
/// Права доступу — місце, де помилка коштує найдорожче, і водночас те, що
/// модульними тестами не перевіряється взагалі: їх дає атрибут [Authorize] над
/// дією, а він працює лише всередині справжнього конвеєра запиту.
/// </summary>
[Collection(ApiGroup.Name)]
public class AccessAndErrorTests(ApiFixture api)
{
    [Theory]
    [InlineData("/api/auth/me")]
    [InlineData("/api/favorites")]
    [InlineData("/api/listings/mine")]
    [InlineData("/api/chat/conversations")]
    [InlineData("/api/billing/wallet")]
    [InlineData("/api/admin/stats")]
    public async Task A_guest_is_turned_away_from_private_places(string path)
    {
        using var response = await api.Client.GetAsync(new Uri(path, UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Звичайний користувач в адмінку не заходить. Важлива саме різниця між
    /// 401 і 403: перше означає «ти не представився», друге — «представився,
    /// але тобі не можна». Плутанина тут відправляла б людину входити ще раз.
    /// </summary>
    [Fact]
    public async Task An_ordinary_person_is_not_an_administrator()
    {
        var token = await RegisterAsync();

        using var request = Authorized(HttpMethod.Get, "/api/admin/stats", token);
        using var response = await api.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task The_administrator_from_the_seed_can_get_in()
    {
        var token = await AdminTokenAsync();

        using var request = Authorized(HttpMethod.Get, "/api/admin/stats", token);
        using var response = await api.Client.SendAsync(request);

        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Неіснуюче оголошення — це 404, а не п'ятисотка з нутрощів бази.
    ///
    /// Сусіднє правило — «чуже неопубліковане теж віддає 404, а не 403» —
    /// перевіряється поки що лише модульно (ListingDraftTests). Щоб дістати
    /// його звідси, потрібен повний шлях створення оголошення через HTTP;
    /// це наступний крок.
    /// </summary>
    [Fact]
    public async Task A_listing_that_does_not_exist_gives_a_plain_404()
    {
        using var response = await api.Client.GetAsync(
            new Uri("/api/listings/999999999", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Поганий запит має повертати розібрані помилки по полях, а не суху
    /// чотирисотку: саме з них фронтенд малює підказки під полями форми.
    /// </summary>
    [Fact]
    public async Task A_broken_request_comes_back_with_errors_per_field()
    {
        using var response = await api.Client.PostAsJsonAsync(
            new Uri("/api/auth/register", UriKind.Relative),
            new { email = "не пошта", password = "1", displayName = "", accountType = "Private" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        var errors = problem.GetProperty("errors");

        Assert.Equal(JsonValueKind.Object, errors.ValueKind);
        Assert.NotEmpty(errors.EnumerateObject());
    }

    /// <summary>
    /// Текст помилки приходить мовою запиту. Перевірити це можна лише звідси:
    /// прикладний рівень кидає КОД правила, а в слова його переводить фільтр
    /// на межі застосунку — коли мова вже відома із заголовка.
    /// </summary>
    [Fact]
    public async Task Validation_messages_speak_the_requested_language()
    {
        var ukrainian = await FirstErrorAsync("uk");
        var english = await FirstErrorAsync("en");

        Assert.False(string.IsNullOrWhiteSpace(ukrainian));
        Assert.False(string.IsNullOrWhiteSpace(english));
        Assert.NotEqual(ukrainian, english);

        /*
            І це справді речення, а не сирий код правила, який лишився
            неперекладеним. Коди в проєкті виглядають як «auth.emailInvalid» —
            одне слово без пробілів; будь-яке людське формулювання їх має.
        */
        Assert.Contains(" ", ukrainian, StringComparison.Ordinal);
        Assert.Contains(" ", english, StringComparison.Ordinal);
    }

    // ─────────────────────────── Оснащення ───────────────────────────

    private async Task<string> FirstErrorAsync(string language)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri("/api/auth/register", UriKind.Relative))
        {
            Content = JsonContent.Create(
                new { email = "не пошта", password = "1", displayName = "Хтось", accountType = "Private" }),
        };

        request.Headers.Add("Accept-Language", language);

        using var response = await api.Client.SendAsync(request);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        return problem
            .GetProperty("errors")
            .EnumerateObject()
            .First()
            .Value[0]
            .GetString()!;
    }

    private async Task<string> RegisterAsync()
    {
        using var response = await api.Client.PostAsJsonAsync(
            new Uri("/api/auth/register", UriKind.Relative),
            new
            {
                email = $"visitor-{Guid.NewGuid():N}@autolot.test",
                password = "Integration-Password-1!",
                displayName = "Звичайний Відвідувач",
                accountType = "Private",
            });

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("accessToken")
            .GetString()!;
    }

    private async Task<string> AdminTokenAsync()
    {
        using var response = await api.Client.PostAsJsonAsync(
            new Uri("/api/auth/login", UriKind.Relative),
            new { email = AutoLotApi.AdminEmail, password = AutoLotApi.AdminPassword });

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("accessToken")
            .GetString()!;
    }

    private static HttpRequestMessage Authorized(HttpMethod method, string path, string token)
    {
        var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return request;
    }
}
