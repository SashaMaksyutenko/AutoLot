using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace AutoLot.Tests.Integration;

/// <summary>
/// Шлях оголошення від чернетки до видачі — через справжні запити.
///
/// Це найдовший сценарій у проєкті й той, яким людина користується щодня:
/// подати авто, віддати на модерацію, дочекатися схвалення. Модульні тести
/// перевіряють кожен крок окремо, але не перевіряють, що кроки складаються
/// в ланцюг: що створене повертається за своїм ідентифікатором, що чужий
/// його не бачить, що схвалене з'являється в каталозі.
/// </summary>
[Collection(ApiGroup.Name)]
public class ListingLifecycleTests(ApiFixture api)
{
    [Fact]
    public async Task A_seller_submits_a_car_and_a_moderator_publishes_it()
    {
        var seller = await api.SellerAsync();
        var listingId = await api.DraftAsync(seller);

        // Своє оголошення автор бачить одразу, ще чернеткою.
        var draft = await ReadAsync(listingId, seller);

        Assert.Equal("Draft", draft.GetProperty("status").GetString());

        await SubmitAsync(listingId, seller);
        Assert.Equal("PendingModeration", (await ReadAsync(listingId, seller)).GetProperty("status").GetString());

        await ApproveAsync(listingId);

        // А тепер його видно всім — навіть тому, хто не входив.
        using var response = await api.Client.GetAsync(Listing(listingId));

        response.EnsureSuccessStatusCode();

        var published = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("Active", published.GetProperty("status").GetString());
    }

    /// <summary>
    /// Чужа чернетка для стороннього просто не існує — 404, а не 403.
    ///
    /// Різниця тут не в ввічливості. «403» означало б «таке оголошення є, але
    /// тобі не можна», і за кодами відповідей можна було б перебирати чужі
    /// чернетки, не бачачи їх. Правило перевірялося модульно; тепер видно, що
    /// воно доживає до справжньої відповіді сервера.
    /// </summary>
    [Fact]
    public async Task A_strangers_draft_does_not_exist_for_anyone_else()
    {
        var seller = await api.SellerAsync();
        var stranger = await api.SellerAsync();
        var listingId = await api.DraftAsync(seller);

        using var asStranger = await api.Client.SendAsync(Authorized(HttpMethod.Get, Listing(listingId), stranger));
        using var asGuest = await api.Client.GetAsync(Listing(listingId));

        Assert.Equal(HttpStatusCode.NotFound, asStranger.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, asGuest.StatusCode);
    }

    /// <summary>
    /// Модерувати може не кожен. Без цієї перевірки будь-хто публікував би
    /// собі оголошення сам, оминаючи чергу.
    /// </summary>
    [Fact]
    public async Task An_ordinary_person_cannot_approve_anything()
    {
        var seller = await api.SellerAsync();
        var listingId = await api.DraftAsync(seller);

        await SubmitAsync(listingId, seller);

        using var response = await api.Client.SendAsync(Authorized(
            HttpMethod.Post,
            $"/api/moderation/listings/{listingId}/approve",
            seller));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Порушення доменного правила має приїхати розбірливою відповіддю, а не
    /// п'ятисоткою. Подати на модерацію те, що вже подане, — саме такий випадок:
    /// виняток кидає сутність, а в HTTP його перекладає DomainExceptionHandler.
    /// </summary>
    [Fact]
    public async Task Submitting_twice_is_refused_with_a_readable_answer()
    {
        var seller = await api.SellerAsync();
        var listingId = await api.DraftAsync(seller);

        await SubmitAsync(listingId, seller);

        using var again = await api.Client.SendAsync(Authorized(
            HttpMethod.Post,
            $"/api/listings/{listingId}/submit",
            seller));

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);

        var problem = await again.Content.ReadFromJsonAsync<JsonElement>();
        var detail = problem.GetProperty("detail").GetString();

        // Не порожньо й не сирий код правила — людський текст.
        Assert.False(string.IsNullOrWhiteSpace(detail));
        Assert.Contains(" ", detail, StringComparison.Ordinal);
    }

    /// <summary>
    /// І цей текст теж має говорити мовою запиту: доменні повідомлення йдуть
    /// тим самим шляхом «код правила → словник», що й помилки валідації.
    /// </summary>
    [Fact]
    public async Task Domain_messages_speak_the_requested_language()
    {
        var seller = await api.SellerAsync();

        var ukrainian = await RefusalAsync(seller, "uk");
        var english = await RefusalAsync(seller, "en");

        Assert.NotEqual(ukrainian, english);
    }

    // ─────────────────────────── Оснащення ───────────────────────────

    private async Task<string> RefusalAsync(string token, string language)
    {
        var listingId = await api.DraftAsync(token);

        await SubmitAsync(listingId, token);

        var request = Authorized(HttpMethod.Post, $"/api/listings/{listingId}/submit", token);
        request.Headers.Add("Accept-Language", language);

        using var response = await api.Client.SendAsync(request);

        return (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("detail")
            .GetString()!;
    }

    private async Task SubmitAsync(long listingId, string token)
    {
        using var response = await api.Client.SendAsync(Authorized(
            HttpMethod.Post,
            $"/api/listings/{listingId}/submit",
            token));

        response.EnsureSuccessStatusCode();
    }

    private async Task ApproveAsync(long listingId)
    {
        using var response = await api.Client.SendAsync(Authorized(
            HttpMethod.Post,
            $"/api/moderation/listings/{listingId}/approve",
            await api.AdminTokenAsync()));

        response.EnsureSuccessStatusCode();
    }

    private async Task<JsonElement> ReadAsync(long listingId, string token)
    {
        using var response = await api.Client.SendAsync(Authorized(HttpMethod.Get, Listing(listingId), token));

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static string Listing(long listingId) => $"/api/listings/{listingId}";

    private static HttpRequestMessage Authorized(HttpMethod method, string path, string token)
    {
        var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return request;
    }
}
