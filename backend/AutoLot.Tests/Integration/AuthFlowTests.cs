using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace AutoLot.Tests.Integration;

/// <summary>
/// Вхід наскрізь: реєстрація, токен, захищений маршрут, поновлення сесії.
///
/// Саме той шар, якого модульні тести не бачать. Сервіс автентифікації вони
/// перевіряють, а от чи доїхав токен у заголовку, чи прочитав його middleware,
/// чи поклав сервер refresh-токен у cookie з правильними ознаками — про це
/// знає лише справжній HTTP.
/// </summary>
[Collection(ApiGroup.Name)]
public class AuthFlowTests(ApiFixture api)
{
    [Fact]
    public async Task A_new_person_registers_and_gets_a_working_token()
    {
        var client = api.NewClient();
        var person = await RegisterAsync(client, "newcomer");

        // Токен працює: захищений маршрут віддає профіль саме цієї людини.
        using var request = Authorized(HttpMethod.Get, "/api/auth/me", person.AccessToken);
        using var response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();

        var profile = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(person.Email, profile.GetProperty("email").GetString());
    }

    /// <summary>
    /// Refresh-токен у тілі відповіді не повертається НІКОЛИ — лише в cookie,
    /// недоступній для JavaScript. Це головна причина, чому чужий скрипт на
    /// сторінці не може вкрасти сесію надовго.
    /// </summary>
    [Fact]
    public async Task The_refresh_token_never_travels_in_the_body()
    {
        var client = api.NewClient();

        using var response = await client.PostAsJsonAsync(
            new Uri("/api/auth/register", UriKind.Relative),
            NewPerson("secretive"));

        var body = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("refresh", body, StringComparison.OrdinalIgnoreCase);

        // А в cookie — є, і саме з ознакою httpOnly.
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));

        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/auth", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_wrong_password_is_refused()
    {
        var client = api.NewClient();
        var person = await RegisterAsync(client, "forgetful");

        using var response = await client.PostAsJsonAsync(
            new Uri("/api/auth/login", UriKind.Relative),
            new { email = person.Email, password = "Definitely-Wrong-1!" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task The_same_mailbox_cannot_register_twice()
    {
        var client = api.NewClient();
        var person = await RegisterAsync(client, "twice");

        using var response = await client.PostAsJsonAsync(
            new Uri("/api/auth/register", UriKind.Relative),
            NewPerson("twice") with { email = person.Email });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    /// <summary>
    /// Поновлення сесії: клієнт не надсилає нічого, cookie їде сама. Саме так
    /// це працює у фронтенді, коли п'ятнадцятихвилинний токен закінчився.
    /// </summary>
    [Fact]
    public async Task The_session_renews_itself_from_the_cookie()
    {
        var client = api.NewClient();
        var first = await RegisterAsync(client, "returning");

        using var response = await client.PostAsync(
            new Uri("/api/auth/refresh", UriKind.Relative),
            content: null);

        response.EnsureSuccessStatusCode();

        var renewed = await response.Content.ReadFromJsonAsync<JsonElement>();
        var token = renewed.GetProperty("accessToken").GetString();

        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.NotEqual(first.AccessToken, token);
    }

    /// <summary>
    /// Без cookie поновлювати нічого. Відповідь має бути 401, а не 500 —
    /// відсутня сесія це звичайний стан, а не поломка.
    /// </summary>
    [Fact]
    public async Task Renewal_without_a_session_is_refused_politely()
    {
        using var response = await api.NewClient().PostAsync(
            new Uri("/api/auth/refresh", UriKind.Relative),
            content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Signing_out_clears_the_session()
    {
        var client = api.NewClient();
        var person = await RegisterAsync(client, "leaving");

        using var request = Authorized(HttpMethod.Post, "/api/auth/logout", person.AccessToken);
        using var goodbye = await client.SendAsync(request);

        goodbye.EnsureSuccessStatusCode();

        // Сесії більше немає — поновити її вже не вийде.
        using var afterwards = await client.PostAsync(
            new Uri("/api/auth/refresh", UriKind.Relative),
            content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, afterwards.StatusCode);
    }

    // ─────────────────────────── Оснащення ───────────────────────────

    private static async Task<(string Email, string AccessToken)> RegisterAsync(
        HttpClient client,
        string nickname)
    {
        var person = NewPerson(nickname);

        using var response = await client.PostAsJsonAsync(
            new Uri("/api/auth/register", UriKind.Relative),
            person);

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        return (person.email, payload.GetProperty("accessToken").GetString()!);
    }

    /// <summary>
    /// Пошта унікальна для кожного виклику: тести ділять одну базу, і двоє
    /// «newcomer@autolot.test» посварилися б за той самий рядок.
    /// </summary>
    private static Person NewPerson(string nickname) => new(
        $"{nickname}-{Guid.NewGuid():N}@autolot.test",
        "Integration-Password-1!",
        "Тестова Людина",
        "Private",
        null);

    private static HttpRequestMessage Authorized(HttpMethod method, string path, string token)
    {
        var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return request;
    }

    /// <summary>
    /// Тіло запиту як запис із малих літер: саме так його чекає сервер, і
    /// писати анонімний об'єкт у кожному тесті було б зайвим повторенням.
    /// </summary>
    private sealed record Person(
        string email,
        string password,
        string displayName,
        string accountType,
        string? phoneNumber);
}
