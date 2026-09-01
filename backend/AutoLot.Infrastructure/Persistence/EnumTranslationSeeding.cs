using AutoLot.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace AutoLot.Infrastructure.Persistence;

/// <summary>
/// Один елемент сід-файла перелічень: значення в коді та його назви мовами.
/// Живе тут, а не поруч із довідниками авто, бо форма однакова для будь-якого
/// перелічення — кузова, палива, причини скарги.
/// </summary>
internal sealed record EnumValueSeed
{
    public string Value { get; init; } = string.Empty;

    public Dictionary<string, string> Names { get; init; } = [];
}

/// <summary>
/// Форма будь-якого файла з назвами перелічень: «ім'я типу → перелік значень».
/// Порядок елементів у масиві визначений, і саме він стає порядком у
/// випадаючому списку.
/// </summary>
internal record EnumSeedDocument
{
    public Dictionary<string, IReadOnlyList<EnumValueSeed>> Enums { get; init; } = [];
}

/// <summary>
/// Переносить назви значень перелічень із сід-файла в таблицю перекладів.
///
/// Винесено окремо тієї ж миті, коли з'явився другий такий файл: цикл
/// однаковий для кузовів і для причин скарги, а копія почала б розходитися
/// з оригіналом при першій же правці.
/// </summary>
internal static class EnumTranslationSeeding
{
    /// <summary>
    /// Оновлює наявні переклади й додає відсутні. Повторний запуск безпечний:
    /// звіряння йде за ключем «тип + значення + мова», тож нових рядків не
    /// з'являється.
    /// </summary>
    public static async Task ApplyAsync(
        AutoLotDbContext dbContext,
        IReadOnlyDictionary<string, IReadOnlyList<EnumValueSeed>> enums,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(enums);

        // Тягнемо лише ті типи, що є у файлі: інакше сідер причин скарги
        // вантажив би в пам'ять усі кольори й кузови заради шести рядків.
        var names = enums.Keys.ToList();

        var existing = await dbContext.EnumTranslations
            .Where(translation => names.Contains(translation.EnumName))
            .ToDictionaryAsync(
                translation => (translation.EnumName, translation.Value, translation.Language),
                cancellationToken);

        foreach (var (enumName, values) in enums)
        {
            for (var sortOrder = 0; sortOrder < values.Count; sortOrder++)
            {
                // Порядок у файлі і є порядком у списку: перший кузов у JSON
                // буде першим у випадаючому списку.
                var value = values[sortOrder].Value;

                foreach (var (rawLanguage, name) in values[sortOrder].Names)
                {
                    var languageCode = LanguageCodes.Normalize(rawLanguage);
                    var key = (enumName, value, languageCode);

                    if (!existing.TryGetValue(key, out var translation))
                    {
                        translation = new EnumTranslation
                        {
                            EnumName = enumName,
                            Value = value,
                            Language = languageCode,
                        };

                        dbContext.EnumTranslations.Add(translation);
                        existing[key] = translation;
                    }

                    translation.Name = name;
                    translation.SortOrder = sortOrder;
                }
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
