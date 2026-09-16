using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AutoLot.Tests.TestDoubles;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace AutoLot.Tests.Integration;

/// <summary>
/// Піднімає ВЕСЬ застосунок у пам'яті — так, як він піднімається на сервері.
///
/// Чим це відрізняється від решти тестів. Модульні тести перевіряють правила:
/// чи правильно рахується крок ставки, чи можна відгукнутися про непродане
/// авто. Вони нічого не знають про HTTP і саме тому швидкі. Але між правилом
/// і відповіддю сервера лежить ще половина застосунку: маршрутизація,
/// перевірка токена, фільтр валідації, перетворювач винятків у ProblemDetails,
/// вибір мови за Accept-Language. Помилка в будь-якій із цих ланок не зачепить
/// жодного модульного тесту — і приїде до користувача.
///
/// Тому тут HTTP справжній: справжні коди відповідей, справжні заголовки,
/// справжній JSON. Мережі при цьому немає — клієнт говорить із застосунком
/// напряму, в межах одного процесу.
/// </summary>
internal sealed class AutoLotApi : WebApplicationFactory<Program>
{
    /// <summary>
    /// Ключ підпису токенів для тестів. Довжина важлива: коротший за 32 символи
    /// застосунок не прийме, і тести падали б на старті з незрозумілої причини.
    /// </summary>
    private const string SigningKey = "integration-tests-signing-key-32-bytes-min";

    public const string AdminEmail = "admin@autolot.test";
    public const string AdminPassword = "Admin-Password-1!";

    private readonly string connectionString;
    private readonly int authPermitLimit;

    /// <param name="authPermitLimit">
    /// Скільки спроб за хвилину дозволити на /api/auth. За замовчуванням
    /// навмисно багато — див. пояснення нижче; низьке значення потрібне лише
    /// тому тесту, який перевіряє сам обмежувач.
    /// </param>
    public AutoLotApi(string connectionString, int authPermitLimit = 10_000)
    {
        this.connectionString = connectionString;
        this.authPermitLimit = authPermitLimit;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        /*
            Середовище «Testing» — не Development і не Production, і це важливо
            для обох сторін.

            Не Development: там застосунок читає user-secrets розробника. Тести
            підхопили б справжній рядок підключення до робочої бази й справжній
            токен телеграм-бота — і бот почав би опитувати сервери Telegram
            посеред прогону.

            Не Production: там вмикається перенаправлення на HTTPS, і кожна
            відповідь перетворювалася б на 307 замість того, що ми перевіряємо.
        */
        builder.UseEnvironment("Testing");

        foreach (var (key, value) in Settings())
        {
            builder.UseSetting(key, value);
        }
    }

    private Dictionary<string, string?> Settings() =>
        new()
        {
            // Тимчасова схема в справжньому PostgreSQL — див. PostgresTestDatabase.
            ["ConnectionStrings:AutoLot"] = connectionString,

            ["Jwt:Key"] = SigningKey,

            // Адміністратор потрібен, щоб було ким перевіряти закриті розділи.
            ["Seed:Admin:Email"] = AdminEmail,
            ["Seed:Admin:Password"] = AdminPassword,

            // Двісті вигаданих оголошень із намальованими фото піднімали б
            // застосунок хвилину. Тести створюють собі дані самі.
            ["DemoData:Enabled"] = "false",

            // Схему створює PostgresTestDatabase зі самої моделі EF, тож
            // накочувати міграції поверх неї не треба й не можна.
            ["Database:MigrateOnStartup"] = "false",

            ["Https:Redirect"] = "false",

            // Зібраного фронтенду в тестах немає — і не треба: перевіряємо API.
            ["Frontend:DistPath"] = string.Empty,

            /*
                Межа спроб входу: на сервері десять за хвилину, тут навмисно
                багато. У тестовому середовищі адреса клієнта одна на всіх,
                тож усі звернення до /api/auth ділили б один рахунок, і
                половина тестів отримувала б 429 замість перевірки.
                */
            ["RateLimiting:AuthPermitLimit"] = authPermitLimit.ToString(CultureInfo.InvariantCulture),
        };
}

/// <summary>
/// Спільне оснащення для всіх інтеграційних тестів: одна тимчасова схема в базі
/// й один піднятий застосунок на всіх.
///
/// Чому спільне. Старт коштує дорого: створити схему, накотити довідники —
/// майже п'ятсот міст, чотири сотні моделей, тарифи, ролі, адміністратор.
/// Робити це для кожного тесту означало б прогін на кілька хвилин.
/// </summary>
public sealed class ApiFixture : IAsyncLifetime, IAsyncDisposable
{
    private PostgresTestDatabase database = null!;
    private AutoLotApi api = null!;

    /// <summary>Клієнт без автоматичних перенаправлень: коди відповідей ми перевіряємо самі.</summary>
    public HttpClient Client { get; private set; } = null!;

    /// <summary>
    /// Та сама тимчасова схема. Потрібна тому тесту, який піднімає власний
    /// застосунок з іншими налаштуваннями: створювати заради нього ще одну
    /// схему було б марною роботою.
    /// </summary>
    public string ConnectionString => database.ConnectionString;

    public async Task InitializeAsync()
    {
        database = await PostgresTestDatabase.CreateAsync();
        api = new AutoLotApi(database.ConnectionString);

        Client = api.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,

            // Cookie потрібні для перевірки входу: refresh-токен живе саме в них.
            HandleCookies = true,
        });
    }

    /*
        Два способи звільнити те саме, і обидва потрібні.

        DisposeAsync без ValueTask — це IAsyncLifetime, інтерфейс xUnit: саме
        його кличе рушій тестів, коли група добігла кінця.

        Другий, явно реалізований, — загальний IAsyncDisposable мови. Його
        вимагає аналізатор: клас тримає поля, які треба звільняти, і мусить
        уміти це робити за загальним правилом, а не лише на прохання xUnit.
        Імена в них збігаються, тому другий записаний через ім'я інтерфейсу —
        інакше компілятор не розрізнив би, який з них викликають.
    */
    public async Task DisposeAsync()
    {
        Client?.Dispose();

        if (api is not null)
        {
            await api.DisposeAsync();
        }

        if (database is not null)
        {
            await database.DisposeAsync();
        }
    }

    ValueTask IAsyncDisposable.DisposeAsync() => new(DisposeAsync());

    /// <summary>Свіжий клієнт із власним сховищем cookie — для окремої «людини».</summary>
    public HttpClient NewClient() => api.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        HandleCookies = true,
    });

    // ─────────────────────── Спільні кроки для тестів ───────────────────────

    /// <summary>
    /// Реєструє нову людину й повертає її токен.
    ///
    /// Пошта унікальна для кожного виклику: тести ділять одну базу, і двоє
    /// однакових адрес посварилися б за той самий рядок.
    /// </summary>
    public async Task<string> SellerAsync()
    {
        using var response = await Client.PostAsJsonAsync(
            new Uri("/api/auth/register", UriKind.Relative),
            new
            {
                email = $"seller-{Guid.NewGuid():N}@autolot.test",
                password = "Integration-Password-1!",
                displayName = "Тестовий Продавець",
                accountType = "Private",
            });

        response.EnsureSuccessStatusCode();

        return await TokenAsync(response);
    }

    /// <summary>Токен адміністратора, якого створює сід на старті застосунку.</summary>
    public async Task<string> AdminTokenAsync()
    {
        using var response = await Client.PostAsJsonAsync(
            new Uri("/api/auth/login", UriKind.Relative),
            new { email = AutoLotApi.AdminEmail, password = AutoLotApi.AdminPassword });

        response.EnsureSuccessStatusCode();

        return await TokenAsync(response);
    }

    /// <summary>
    /// Ідентифікатори з довідників, потрібні, щоб створити оголошення.
    ///
    /// Беремо їх через API, а не з бази: по-перше, так заразом перевіряються
    /// самі довідники, по-друге, тест не мусить знати про схему. Значення
    /// запам'ятовуємо — довідники за час прогону не змінюються.
    /// </summary>
    public async Task<(long CityId, long MakeId, long ModelId)> ReferenceAsync()
    {
        if (reference is { } cached)
        {
            return cached;
        }

        var regionId = await FirstIdAsync("/api/geo/regions");
        var cityId = await FirstIdAsync($"/api/geo/regions/{regionId}/cities");

        var makeId = await FirstIdAsync("/api/cars/makes");
        var modelId = await FirstIdAsync($"/api/cars/makes/{makeId}/models");

        reference = (cityId, makeId, modelId);

        return reference.Value;
    }

    /// <summary>
    /// Створює чернетку оголошення від імені власника токена й повертає її
    /// ідентифікатор. Мінімальний набір полів, який приймає валідатор.
    /// </summary>
    /// <param name="vin">
    /// Номер кузова; за замовчуванням не вказаний. Якщо передаєте свій — його
    /// десята позиція має відповідати 2021 року, інакше валідатор відмовить.
    /// </param>
    public async Task<long> DraftAsync(string token, string? vin = null)
    {
        var (cityId, makeId, modelId) = await ReferenceAsync();

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri("/api/listings", UriKind.Relative))
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) },
            Content = JsonContent.Create(new
            {
                title = "Honda Pilot 2021",
                description = "Оголошення, створене інтеграційним тестом.",
                cityId,
                price = 25_000m,
                currency = "Usd",
                type = "FixedPrice",
                car = new
                {
                    vin,
                    year = 2021,
                    condition = "Used",
                    makeId,
                    modelId,
                    mileage = 60_000,
                    ownerCount = 1,
                    fuelType = "Petrol",
                    engineVolume = 3.5m,
                    enginePower = 280,
                    transmission = "Automatic",
                    drivetrain = "AllWheel",
                    bodyType = "Crossover",
                    color = "Black",
                    seatCount = 7,
                    doorCount = 5,
                },
            }),
        };

        using var response = await Client.SendAsync(request);

        var body = await response.Content.ReadAsStringAsync();

        // Тіло відповіді в повідомленні про невдачу: інакше «очікували 201,
        // отримали 400» не каже нічого про те, яке поле сервер не прийняв.
        Assert.True(response.StatusCode == System.Net.HttpStatusCode.Created, body);

        return JsonDocument.Parse(body).RootElement.GetProperty("id").GetInt64();
    }

    private (long CityId, long MakeId, long ModelId)? reference;

    private async Task<long> FirstIdAsync(string path)
    {
        var items = await Client.GetFromJsonAsync<JsonElement>(new Uri(path, UriKind.Relative));

        Assert.True(items.GetArrayLength() > 0, $"довідник {path} порожній");

        return items[0].GetProperty("id").GetInt64();
    }

    private static async Task<string> TokenAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("accessToken")
            .GetString()!;
}

/// <summary>
/// Позначка, яка зводить усі інтеграційні тести в одну групу зі спільним
/// оснащенням. Без неї xUnit підняв би застосунок окремо для кожного класу.
/// </summary>
[CollectionDefinition(Name)]
public sealed class ApiGroup : ICollectionFixture<ApiFixture>
{
    public const string Name = "api";
}
