using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AutoLot.Infrastructure.Listings;

namespace AutoLot.Tests.Integration;

/// <summary>
/// Два місця, куди звичайним запитом із JSON не дістатися.
///
/// Перше — завантаження фото: єдиний у проєкті шлях, де тіло запиту не JSON,
/// а багаточастинна форма, і єдиний, де по HTTP виконується обробка зображень.
/// Саме та бібліотека, якої бракувало в збірці під Linux.
///
/// Друге — обмежувач спроб входу. Він живе в конвеєрі, а не в сервісі, тож
/// перевірити його можна лише справжніми запитами поспіль.
/// </summary>
[Collection(ApiGroup.Name)]
public class PhotoAndRateLimitTests(ApiFixture api)
{
    [Fact]
    public async Task A_photo_travels_all_the_way_into_the_listing()
    {
        var seller = await api.SellerAsync();
        var listingId = await api.DraftAsync(seller);

        using var upload = await UploadAsync(listingId, seller, RealImage(), "car.jpg", "image/jpeg");

        var body = await upload.Content.ReadAsStringAsync();

        Assert.True(upload.StatusCode == HttpStatusCode.Created, body);

        var photo = JsonDocument.Parse(body).RootElement;

        // Сервер зберігає ДВІ копії: повнорозмірну й мініатюру для списків.
        Assert.False(string.IsNullOrWhiteSpace(photo.GetProperty("path").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(photo.GetProperty("thumbnailPath").GetString()));

        // Перше фото стає головним саме собою — окремо про це просити не треба.
        Assert.True(photo.GetProperty("isPrimary").GetBoolean());

        var photos = await api.Client.SendAsync(Authorized(
            HttpMethod.Get,
            $"/api/listings/{listingId}/photos",
            seller));

        Assert.Equal(1, (await photos.Content.ReadFromJsonAsync<JsonElement>()).GetArrayLength());

        photos.Dispose();
    }

    /// <summary>
    /// Файл, який лише вдає зображення, приймати не можна: тип визначається за
    /// ВМІСТОМ, а не за розширенням чи заголовком, які надсилає клієнт.
    /// </summary>
    [Fact]
    public async Task A_file_that_only_pretends_to_be_a_photo_is_refused()
    {
        var seller = await api.SellerAsync();
        var listingId = await api.DraftAsync(seller);

        var notAnImage = Encoding.UTF8.GetBytes("Це звичайний текст, а не картинка.");

        using var upload = await UploadAsync(listingId, seller, notAnImage, "car.jpg", "image/jpeg");

        Assert.Equal(HttpStatusCode.BadRequest, upload.StatusCode);
    }

    /// <summary>
    /// Чуже оголошення чужими фото не поповнюють.
    ///
    /// Код відповіді тут 403, а не 404 — і це навмисна різниця між ЧИТАННЯМ
    /// і ЗМІНОЮ. Читання чужої чернетки прикидається, що її немає (див.
    /// ListingLifecycleTests), бо інакше за кодами відповідей можна було б
    /// перебирати чужі чернетки. Зміна відповідає чесним «вам не можна».
    ///
    /// Сама по собі ця пара лишає вузеньку шпарину: 403 на запис усе-таки
    /// підтверджує, що оголошення з таким номером існує. Тест фіксує те, як
    /// воно поводиться СЬОГОДНІ; чи зводити обидва шляхи до 404 — рішення,
    /// яке міняє поведінку API, а не тестів.
    /// </summary>
    [Fact]
    public async Task Nobody_adds_photos_to_a_strangers_listing()
    {
        var seller = await api.SellerAsync();
        var stranger = await api.SellerAsync();
        var listingId = await api.DraftAsync(seller);

        using var upload = await UploadAsync(listingId, stranger, RealImage(), "car.jpg", "image/jpeg");

        Assert.Equal(HttpStatusCode.Forbidden, upload.StatusCode);
    }

    /// <summary>
    /// Обмеження спроб входу (SPEC §8). Перевіряємо його на окремому застосунку
    /// з низькою межею: у спільному вона навмисно піднята, інакше решта тестів
    /// вичерпувала б її одна за одну.
    ///
    /// Межа ділиться за адресою клієнта, а в тестовому сервері вона одна на
    /// всіх — тож тут це навіть зручно: всі запити потрапляють в один рахунок.
    /// </summary>
    [Fact]
    public async Task Too_many_sign_in_attempts_are_cut_off()
    {
        const int limit = 3;

        await using var strict = new AutoLotApi(api.ConnectionString, authPermitLimit: limit);

        using var client = strict.CreateClient();

        var attempt = new { email = "nobody@autolot.test", password = "Wrong-Password-1!" };

        // Перші спроби мають доходити до перевірки й чесно відмовляти.
        for (var number = 1; number <= limit; number++)
        {
            using var allowed = await client.PostAsJsonAsync(
                new Uri("/api/auth/login", UriKind.Relative),
                attempt);

            Assert.Equal(HttpStatusCode.Unauthorized, allowed.StatusCode);
        }

        // А наступна вже не доходить.
        using var blocked = await client.PostAsJsonAsync(
            new Uri("/api/auth/login", UriKind.Relative),
            attempt);

        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);

        // І сервер каже, коли можна повернутися.
        Assert.NotNull(blocked.Headers.RetryAfter);
    }

    // ─────────────────────────── Оснащення ───────────────────────────

    private Task<HttpResponseMessage> UploadAsync(
        long listingId,
        string token,
        byte[] content,
        string fileName,
        string contentType)
    {
        /*
            Багаточастинна форма — те саме, що надсилає браузер, коли людина
            обирає файл. Ім'я поля має збігатися з іменем аргументу дії
            (IFormFile file), інакше сервер просто не побачить файла.
        */
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(content);

        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "file", fileName);

        var request = Authorized(HttpMethod.Post, $"/api/listings/{listingId}/photos", token);

        request.Content = form;

        return api.Client.SendAsync(request);
    }

    /// <summary>
    /// Справжнє зображення беремо в того самого генератора заглушок, яким
    /// користуються демо-дані: вигадувати байти JPEG вручну не варто, а будь-яка
    /// картинка з диска зробила б тест залежним від файлів поруч.
    /// </summary>
    private static byte[] RealImage() =>
        PlaceholderImageFactory.Create("Honda", "Pilot", 2021, photoIndex: 0, seed: 7);

    private static HttpRequestMessage Authorized(HttpMethod method, string path, string token)
    {
        var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return request;
    }
}
