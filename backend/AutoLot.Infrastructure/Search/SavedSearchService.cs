using System.Text.Json;
using AutoLot.Application.Catalog;
using AutoLot.Application.Common.Abstractions;
using AutoLot.Application.Search;
using AutoLot.Application.Search.Dtos;
using AutoLot.Domain.Common;
using AutoLot.Domain.Search;
using AutoLot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AutoLot.Infrastructure.Search;

/// <summary>
/// Збережені пошуки.
///
/// Фільтри лежать у базі рядком JSON, тож усе зводиться до двох перетворень:
/// зберегти об'єкт запиту в текст і прочитати текст назад. Читання завжди
/// захищене — вміст текстового стовпця база не перевіряє, і один зіпсований
/// рядок не має ламати весь список людини.
/// </summary>
internal sealed class SavedSearchService(
    AutoLotDbContext dbContext,
    IDateTimeProvider clock,
    ICatalogService catalog) : ISavedSearchService
{
    /// <summary>
    /// Ті самі правила, що й у решті застосунку: імена властивостей у JSON
    /// з малої літери, у типах — з великої.
    /// </summary>
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<IReadOnlyList<SavedSearchCard>> GetMineAsync(
        long userId,
        CancellationToken cancellationToken = default)
    {
        var searches = await dbContext.SavedSearches
            .AsNoTracking()
            .Where(item => item.UserId == userId)
            .OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.Id)
            .ToListAsync(cancellationToken);

        var cards = new List<SavedSearchCard>(searches.Count);

        foreach (var search in searches)
        {
            cards.Add(await ToCardAsync(search, cancellationToken));
        }

        return cards;
    }

    public async Task<SavedSearchCard> SaveAsync(
        long userId,
        string name,
        CatalogQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var count = await dbContext.SavedSearches
            .CountAsync(item => item.UserId == userId, cancellationToken);

        if (count >= SavedSearch.PerUserLimit)
        {
            throw new DomainRuleException(
                $"Більше {SavedSearch.PerUserLimit} збережених пошуків тримати не можна. " +
                "Видаліть непотрібні.");
        }

        var now = clock.UtcNow;

        var search = new SavedSearch
        {
            UserId = userId,
            // Сторінка й розмір сторінки до фільтра не належать: збережений
            // пошук — це «що шукати», а не «на якій сторінці я зупинився».
            QueryJson = Serialize(query),
            CreatedAt = now,
        };

        search.Rename(name, now);

        dbContext.SavedSearches.Add(search);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await ToCardAsync(search, cancellationToken);
    }

    public async Task<SavedSearchCard> RenameAsync(
        long searchId,
        long userId,
        string name,
        CancellationToken cancellationToken = default)
    {
        var search = await LoadOwnAsync(searchId, userId, cancellationToken);

        search.Rename(name, clock.UtcNow);

        await dbContext.SaveChangesAsync(cancellationToken);

        return await ToCardAsync(search, cancellationToken);
    }

    public async Task<SavedSearchCard> SetNotificationsAsync(
        long searchId,
        long userId,
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        var search = await LoadOwnAsync(searchId, userId, cancellationToken);

        search.SetNotifications(enabled, clock.UtcNow);

        await dbContext.SaveChangesAsync(cancellationToken);

        return await ToCardAsync(search, cancellationToken);
    }

    public async Task DeleteAsync(
        long searchId,
        long userId,
        CancellationToken cancellationToken = default)
    {
        var search = await LoadOwnAsync(searchId, userId, cancellationToken);

        dbContext.SavedSearches.Remove(search);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Завантажує пошук, одразу перевіряючи, що він належить тому, хто питає.
    /// Чужому віддаємо «не знайдено», а не «немає доступу»: скільки пошуків
    /// зберіг сусід — не його справа.
    /// </summary>
    private async Task<SavedSearch> LoadOwnAsync(
        long searchId,
        long userId,
        CancellationToken cancellationToken)
    {
        return await dbContext.SavedSearches
            .FirstOrDefaultAsync(
                item => item.Id == searchId && item.UserId == userId,
                cancellationToken)
            ?? throw new SavedSearchNotFoundException(searchId);
    }

    private async Task<SavedSearchCard> ToCardAsync(
        SavedSearch search,
        CancellationToken cancellationToken)
    {
        var query = Deserialize(search.QueryJson);

        // Рахуємо збіги тим самим сервісом, що й звичайний пошук: інакше
        // число в списку розходилося б із тим, що людина побачить, відкривши
        // цей пошук. Просимо одну позицію — потрібна лише загальна кількість.
        var found = await catalog.SearchAsync(
            query with { Page = 1, PageSize = 1 },
            cancellationToken);

        return new SavedSearchCard(
            search.Id,
            search.Name,
            query,
            found.TotalCount,
            search.NotifyByEmail,
            search.CreatedAt);
    }

    private static string Serialize(CatalogQuery query)
    {
        // Сторінку скидаємо: збережений пошук описує, ЩО шукати, а не де
        // людина зупинилася гортати.
        return JsonSerializer.Serialize(
            query with { Page = 1 },
            SerializerOptions);
    }

    /// <summary>
    /// Читає збережені фільтри. Зіпсований рядок повертає порожній запит, а
    /// не виняток: інакше одна зламана стрічка ховала б від людини всі решту
    /// її пошуків.
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
}
