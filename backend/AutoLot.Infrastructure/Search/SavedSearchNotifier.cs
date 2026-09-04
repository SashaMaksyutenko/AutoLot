using System.Text.Json;
using AutoLot.Application.Catalog;
using AutoLot.Application.Common.Abstractions;
using AutoLot.Application.Search;
using AutoLot.Domain.Search;
using AutoLot.Infrastructure.Email;
using AutoLot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoLot.Infrastructure.Search;

/// <summary>
/// Розсилає листи про нові збіги в збережених пошуках.
///
/// Робота влаштована на кожен пошук окремо: беремо його фільтри, додаємо
/// «опубліковано після межі» — і питаємо каталог. Так відповідь завжди
/// збігається з тим, що людина побачить, відкривши цей пошук на сайті.
/// </summary>
internal sealed partial class SavedSearchNotifier(
    AutoLotDbContext dbContext,
    IDateTimeProvider clock,
    ICatalogService catalog,
    IEmailSender email,
    SearchEmails emails,
    ILogger<SavedSearchNotifier> logger) : ISavedSearchNotifier
{
    /// <summary>
    /// Скільки авто показуємо в листі. Решту згадуємо числом: лист із
    /// сорока картками ніхто не дочитає, а посилання на сам пошук покаже
    /// все одно все.
    /// </summary>
    private const int PerLetter = 5;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<int> NotifyAsync(CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;

        var searches = await dbContext.SavedSearches
            .Include(search => search.User)
            .Where(search => search.NotifyByEmail)
            .ToListAsync(cancellationToken);

        var sent = 0;

        foreach (var search in searches)
        {
            // Один невдалий пошук не має спиняти розсилку решті людей.
            // Найімовірніша причина збою тут — недоступна пошта, і вона
            // ніяк не стосується наступного адресата.
            try
            {
                if (await NotifyOneAsync(search, now, cancellationToken))
                {
                    sent++;
                }
            }
            catch (Exception exception)
            {
                LogFailed(logger, exception, search.Id);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        LogRound(logger, searches.Count, sent);

        return sent;
    }

    /// <summary>
    /// Обробляє один пошук. Повертає <c>true</c>, якщо лист таки пішов.
    /// </summary>
    private async Task<bool> NotifyOneAsync(
        SavedSearch search,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var address = search.User.Email;

        // Непідтверджена скринька — не адреса. Слати на неї означає
        // годувати спам-фільтри й псувати репутацію відправника.
        if (string.IsNullOrEmpty(address) || !search.User.EmailConfirmed)
        {
            return false;
        }

        var query = Deserialize(search.QueryJson) with
        {
            // Межу беремо з самого пошуку; якщо її чомусь немає, вважаємо
            // межею момент створення — гірше за це лише розіслати все.
            PublishedAfter = search.NotifyFrom ?? search.CreatedAt,
            Sort = CatalogSort.Newest,
            Page = 1,
            PageSize = PerLetter,
        };

        var found = await catalog.SearchAsync(query, cancellationToken);

        // Межу зсуваємо в будь-якому разі — навіть коли нічого не знайшлося.
        // Інакше кожен наступний запуск перебирав би все ширший проміжок.
        search.MarkNotified(now);

        if (found.TotalCount == 0)
        {
            return false;
        }

        await email.SendAsync(
            emails.NewMatches(address, search.Name, search.Id, found.Items, found.TotalCount),
            cancellationToken);

        LogSent(logger, search.Id, found.TotalCount);

        return true;
    }

    /// <summary>
    /// Читає збережені фільтри. Зіпсований рядок дає порожній запит, а не
    /// виняток — так само, як у списку пошуків.
    /// </summary>
    private static CatalogQuery Deserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<CatalogQuery>(json, SerializerOptions) ?? new CatalogQuery();
        }
        catch (JsonException)
        {
            return new CatalogQuery();
        }
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Збережені пошуки: перевірено {Examined}, надіслано листів {Sent}")]
    private static partial void LogRound(ILogger logger, int examined, int sent);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Пошук {SearchId}: знайдено {Count} нових, лист надіслано")]
    private static partial void LogSent(ILogger logger, long searchId, int count);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Пошук {SearchId}: сповістити не вдалося")]
    private static partial void LogFailed(ILogger logger, Exception exception, long searchId);
}
