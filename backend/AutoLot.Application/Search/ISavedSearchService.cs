using AutoLot.Application.Catalog;
using AutoLot.Application.Search.Dtos;

namespace AutoLot.Application.Search;

/// <summary>
/// Збережені пошуки користувача.
///
/// Зберігається саме набір фільтрів, а не знайдені оголошення: сенс у тому,
/// щоб побачити те, що з'явилося ПІСЛЯ збереження. Список знайденого
/// застарів би наступного дня.
/// </summary>
public interface ISavedSearchService
{
    /// <summary>Мої пошуки, найновіші першими, з поточною кількістю збігів.</summary>
    Task<IReadOnlyList<SavedSearchCard>> GetMineAsync(
        long userId,
        CancellationToken cancellationToken = default);

    /// <summary>Зберігає поточні фільтри під назвою.</summary>
    Task<SavedSearchCard> SaveAsync(
        long userId,
        string name,
        CatalogQuery query,
        CancellationToken cancellationToken = default);

    Task<SavedSearchCard> RenameAsync(
        long searchId,
        long userId,
        string name,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Вмикає або вимикає листи про нові збіги. Увімкнення рахує «новим»
    /// лише те, що з'явиться далі.
    /// </summary>
    Task<SavedSearchCard> SetNotificationsAsync(
        long searchId,
        long userId,
        bool enabled,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(long searchId, long userId, CancellationToken cancellationToken = default);
}

/// <summary>Такого збереженого пошуку немає — або він чужий.</summary>
public sealed class SavedSearchNotFoundException(long searchId)
    : Exception($"Збережений пошук {searchId} не знайдено.");
