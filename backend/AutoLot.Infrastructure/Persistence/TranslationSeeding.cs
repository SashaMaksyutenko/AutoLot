using AutoLot.Domain.Common;

namespace AutoLot.Infrastructure.Persistence;

/// <summary>
/// Спільна для всіх сідерів дія: перенести назви з сід-файла в таблицю
/// перекладів. Винесена окремо, бо однакова для областей, міст, країн і опцій.
/// </summary>
internal static class TranslationSeeding
{
    /// <summary>
    /// Оновлює наявний переклад або додає новий. Мова зводиться до
    /// підтримуваного коду, тож «uk-UA» у файлі не створить окремий рядок.
    /// </summary>
    public static void Apply<TTranslation>(
        ICollection<TTranslation> translations,
        Dictionary<string, string> names,
        Func<TTranslation> create)
        where TTranslation : Translation
    {
        ArgumentNullException.ThrowIfNull(translations);
        ArgumentNullException.ThrowIfNull(names);
        ArgumentNullException.ThrowIfNull(create);

        foreach (var (rawLanguage, name) in names)
        {
            var language = LanguageCodes.Normalize(rawLanguage);
            var translation = translations.FirstOrDefault(candidate => candidate.Language == language);

            if (translation is null)
            {
                translation = create();
                translation.Language = language;
                translations.Add(translation);
            }

            translation.Name = name;
        }
    }
}
