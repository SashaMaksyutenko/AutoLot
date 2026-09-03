using AutoLot.Application.Common.Abstractions;
using AutoLot.Domain.Billing;
using AutoLot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoLot.Infrastructure.Billing;

/// <summary>
/// Наповнює довідник тарифних планів із вбудованого файла.
///
/// Звіряється за кодом плану, тож повторний запуск оновлює наявні, а не
/// плодить копії. Ціну й ліміт при цьому ПЕРЕЗАПИСУЄ: файл — джерело істини
/// для довідника. На вже оплачені періоди це не впливає — вони зберігають
/// свою ціну окремим полем.
/// </summary>
public sealed partial class PlanSeeder(
    AutoLotDbContext dbContext,
    ILogger<PlanSeeder> logger) : IDataSeeder
{
    private const string ResourceName = "AutoLot.Infrastructure.Persistence.SeedData.plans.json";

    /// <summary>Після довідників модерації; залежностей немає.</summary>
    public int Order => 5;

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var document = await SeedResource.ReadAsync<PlansSeedDocument>(ResourceName, cancellationToken);

        if (document.Plans.Count == 0)
        {
            LogEmpty(logger);

            return;
        }

        // Рівно один план може бути типовим. Помилка в файлі тут коштувала б
        // дорого: два типові — і ліміт залежав би від порядку рядків у базі.
        var defaults = document.Plans.Count(plan => plan.IsDefault);

        if (defaults != 1)
        {
            throw new InvalidOperationException(
                $"У plans.json має бути рівно один план із isDefault, а знайдено {defaults}.");
        }

        var plans = await dbContext.Plans
            .Include(plan => plan.Translations)
            .ToDictionaryAsync(plan => plan.Code, cancellationToken);

        for (var sortOrder = 0; sortOrder < document.Plans.Count; sortOrder++)
        {
            var seed = document.Plans[sortOrder];

            if (!plans.TryGetValue(seed.Code, out var plan))
            {
                plan = new Plan { Code = seed.Code };
                dbContext.Plans.Add(plan);
                plans[seed.Code] = plan;
            }

            plan.Price = seed.Price;
            plan.DurationDays = seed.DurationDays;
            plan.ListingLimit = seed.ListingLimit;
            plan.IsDefault = seed.IsDefault;
            plan.SortOrder = sortOrder;

            TranslationSeeding.Apply(plan.Translations, seed.Names, () => new PlanTranslation());

            // Опис не вкладається в спільний помічник: той знає лише про
            // назву. Тому проходимо описи окремо, по тих самих мовах.
            foreach (var (rawLanguage, description) in seed.Descriptions)
            {
                var languageCode = Domain.Common.LanguageCodes.Normalize(rawLanguage);
                var translation = plan.Translations
                    .FirstOrDefault(item => item.Language == languageCode);

                if (translation is not null)
                {
                    translation.Description = description;
                }
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        LogSeeded(logger, plans.Count);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Тарифних планів: {Plans}")]
    private static partial void LogSeeded(ILogger logger, int plans);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Файл тарифів порожній — довідник не наповнено")]
    private static partial void LogEmpty(ILogger logger);
}

/// <summary>Форма файла plans.json.</summary>
internal sealed record PlansSeedDocument
{
    public IReadOnlyList<PlanSeed> Plans { get; init; } = [];
}

internal sealed record PlanSeed
{
    public string Code { get; init; } = string.Empty;

    public decimal Price { get; init; }

    public int DurationDays { get; init; } = 30;

    /// <summary><c>null</c> — без обмеження.</summary>
    public int? ListingLimit { get; init; }

    public bool IsDefault { get; init; }

    public Dictionary<string, string> Names { get; init; } = [];

    public Dictionary<string, string> Descriptions { get; init; } = [];
}
