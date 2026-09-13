using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using AutoLot.Application.Listings;
using AutoLot.Domain.Enums;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;

namespace AutoLot.Api.Extensions;

/// <summary>
/// Роздача зібраного фронтенду з підстановкою тегів попереднього перегляду.
///
/// НАВІЩО це взагалі потрібно. Коли людина кидає посилання на оголошення в
/// Telegram чи Viber, месенджер іде за ним і читає HTML — але JavaScript він
/// НЕ виконує. Тобто всі теги, які фронтенд проставив би сам, для нього не
/// існують: він бачить порожню оболонку й показує голе посилання замість
/// картки з фото й ціною.
///
/// Єдиний спосіб це виправити — віддати готовий HTML із сервера. Тому, коли
/// задано шлях до зібраного фронтенду, API роздає його сам і для адрес
/// виду /listing/{id} підставляє в оболонку теги саме цього авто.
///
/// У розробці шлях не задають: там сайт роздає Vite, і теги нікому не
/// потрібні — посилання на localhost однаково нікуди не надішлеш.
/// </summary>
internal static partial class SpaHosting
{
    public static IApplicationBuilder UseFrontend(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var root = app.Configuration["Frontend:DistPath"];

        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            return app;
        }

        var files = new PhysicalFileProvider(Path.GetFullPath(root));

        app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = files });
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = files,
            ContentTypeProvider = new FileExtensionContentTypeProvider(),
        });

        // Усе, що не файл і не API, — це маршрут усередині застосунку, і
        // відповідати на нього має та сама оболонка.
        app.MapFallback(async context =>
        {
            var shell = await File.ReadAllTextAsync(
                Path.Combine(files.Root, "index.html"),
                context.RequestAborted);

            var path = context.Request.Path.Value ?? string.Empty;
            var match = ListingPath().Match(path);

            if (match.Success && long.TryParse(match.Groups[1].Value, out var listingId))
            {
                shell = await WithListingPreviewAsync(shell, listingId, context);
            }

            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.WriteAsync(shell, context.RequestAborted);
        });

        return app;
    }

    /// <summary>
    /// Вставляє в оболонку теги конкретного оголошення.
    ///
    /// Неопубліковане авто лишає оболонку як є: чернетка не має проступати
    /// в попередньому перегляді навіть заголовком.
    /// </summary>
    private static async Task<string> WithListingPreviewAsync(
        string shell,
        long listingId,
        HttpContext context)
    {
        var listings = context.RequestServices.GetRequiredService<IListingService>();

        var listing = await listings.GetAsync(listingId, null, false, context.RequestAborted);

        if (listing is null)
        {
            return shell;
        }

        var site = context.RequestServices
            .GetRequiredService<IConfiguration>()["Email:SiteUrl"]
            ?.TrimEnd('/') ?? string.Empty;

        // Картку читають очима, а не програмою, тож ціна має виглядати як
        // ціна: «41 184 $», а не «41184 Usd». Пробіл між тисячами —
        // нерозривний, інакше месенджер розірве число на два рядки.
        var sign = listing.Currency switch
        {
            Currency.Usd => "$",
            Currency.Eur => "€",
            _ => "₴",
        };

        var price = listing.Price.ToString("#,0.##", CultureInfo.GetCultureInfo("uk-UA")) + " " + sign;

        var title = $"{listing.Title} — {price}";

        // Опис ріжемо: месенджери показують перші рядки, а решту все одно
        // відкинуть. Різати краще самим — по цілому слову.
        var description = Shorten(listing.Description, 200);

        var photo = listing.Photos.FirstOrDefault(item => item.IsPrimary)
            ?? listing.Photos.FirstOrDefault();

        var tags = new List<string>
        {
            // Звичайний опис — для пошуковика, og — для месенджерів і
            // соцмереж. Потрібні обидва: вони читають різні теги.
            Meta("description", description),
            Meta("og:type", "website"),
            Meta("og:title", title),
            Meta("og:description", description),
            Meta("og:url", $"{site}/listing/{listing.Id}"),
            Meta("twitter:card", photo is null ? "summary" : "summary_large_image"),
        };

        if (photo is not null)
        {
            tags.Add(Meta("og:image", $"{site}/media/{photo.Path}"));
        }

        // Заголовок вкладки теж підміняємо: інакше в історії браузера всі
        // оголошення називалися б однаково.
        shell = TitleTag().Replace(shell, $"<title>{WebUtility.HtmlEncode(title)}</title>", 1);

        /*
          Загальні теги з оболонки СПЕРШУ прибираємо.

          Дописати свої мало: більшість роботів бере перше знайдене значення,
          а в оболонці воно стоїть вище. Сторінка з двома og:title показала б
          назву сайту замість назви авто — тобто рівно те, заради чого все це
          й робиться, не спрацювало б.
        */
        shell = PreviewTags().Replace(shell, string.Empty);

        return shell.Replace("</head>", string.Join('\n', tags) + "\n</head>", StringComparison.Ordinal);
    }

    /// <summary>
    /// Один тег. Значення проходить через кодування HTML: в описі авто
    /// трапляються лапки, і без цього вони обірвали б атрибут, а разом із
    /// ним і решту сторінки.
    /// </summary>
    private static string Meta(string property, string content)
    {
        var attribute = property.StartsWith("og:", StringComparison.Ordinal) ? "property" : "name";

        return $"    <meta {attribute}=\"{property}\" content=\"{WebUtility.HtmlEncode(content)}\" />";
    }

    private static string Shorten(string text, int limit)
    {
        var clean = text.ReplaceLineEndings(" ").Trim();

        if (clean.Length <= limit)
        {
            return clean;
        }

        var cut = clean[..limit];
        var lastSpace = cut.LastIndexOf(' ');

        return (lastSpace > 0 ? cut[..lastSpace] : cut) + "…";
    }

    /// <summary>
    /// Теги, які замінюємо своїми: опис сторінки й уся розмітка попереднього
    /// перегляду. Загальний опис сайту на картці авто — це опис не того.
    /// </summary>
    [GeneratedRegex("""\s*<meta\s+(?:property|name)="(?:description|og:[^"]*|twitter:[^"]*)"[^>]*>""")]
    private static partial Regex PreviewTags();

    [GeneratedRegex(@"^/listing/(\d+)/?$")]
    private static partial Regex ListingPath();

    [GeneratedRegex(@"<title>.*?</title>", RegexOptions.Singleline)]
    private static partial Regex TitleTag();
}
