using AutoLot.Application.Cars.Dtos;
using AutoLot.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace AutoLot.Infrastructure.Persistence;

/// <summary>Один рядок таблиці перекладів, як він потрібен для показу.</summary>
internal sealed record TranslationRow(
    string EnumName,
    string Value,
    string Name,
    string Language,
    int SortOrder);

/// <summary>
/// Дістає з таблиці перекладів назви значень перелічень для випадаючих
/// списків.
///
/// Читання розділене надвоє навмисно. <see cref="LoadAsync"/> ходить у базу
/// один раз за всі потрібні типи, а <see cref="Pick"/> уже розкладає готові
/// рядки в пам'яті. Інакше довідник із п'яти перелічень коштував би п'ять
/// запитів замість одного.
/// </summary>
internal static class EnumTranslationLookup
{
    /// <summary>
    /// Тягне переклади вказаних типів двома мовами: потрібною та типовою.
    /// Друга — запасний варіант, якщо назви потрібною ще не додали.
    /// </summary>
    public static async Task<IReadOnlyList<TranslationRow>> LoadAsync(
        AutoLotDbContext dbContext,
        IReadOnlyList<string> enumNames,
        string language,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        return await dbContext.EnumTranslations
            .AsNoTracking()
            .Where(translation => enumNames.Contains(translation.EnumName)
                && (translation.Language == language
                    || translation.Language == LanguageCodes.Default))
            .Select(translation => new TranslationRow(
                translation.EnumName,
                translation.Value,
                translation.Name,
                translation.Language,
                translation.SortOrder))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Вибирає значення одного перелічення потрібною мовою. Якщо перекладу
    /// немає, лишається той, що знайшовся — тобто українська.
    /// </summary>
    public static IReadOnlyList<LookupItem> Pick(
        IReadOnlyList<TranslationRow> rows,
        string enumName,
        string language)
    {
        ArgumentNullException.ThrowIfNull(rows);

        return
        [
            .. rows
                .Where(row => row.EnumName == enumName)
                .GroupBy(row => row.Value)
                .Select(group => group.FirstOrDefault(row => row.Language == language) ?? group.First())
                .OrderBy(row => row.SortOrder)
                .ThenBy(row => row.Name, StringComparer.CurrentCulture)
                .Select(row => new LookupItem(row.Value, row.Name)),
        ];
    }

    /// <summary>
    /// Зручний випадок «одне перелічення»: сам сходить у базу й сам розкладе.
    /// </summary>
    public static async Task<IReadOnlyList<LookupItem>> GetAsync(
        AutoLotDbContext dbContext,
        string enumName,
        string language,
        CancellationToken cancellationToken = default)
    {
        var rows = await LoadAsync(dbContext, [enumName], language, cancellationToken);

        return Pick(rows, enumName, language);
    }
}
