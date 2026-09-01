using AutoLot.Application.Common.Abstractions;
using AutoLot.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace AutoLot.Infrastructure.Listings;

/// <summary>
/// Наповнює довідники модерації — поки що це лише назви причин скарги.
///
/// Окремий сідер, а не рядок у довіднику авто: причина скарги не є
/// характеристикою автомобіля, і класти її в car-attributes.json означало б
/// зберігати дані там, де їх ніхто не шукатиме.
/// </summary>
public sealed partial class ModerationSeeder(
    AutoLotDbContext dbContext,
    ILogger<ModerationSeeder> logger) : IDataSeeder
{
    private const string ResourceName = "AutoLot.Infrastructure.Persistence.SeedData.moderation.json";

    /// <summary>Після довідників авто; залежностей немає, порядок лише заради логів.</summary>
    public int Order => 4;

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var document = await SeedResource.ReadAsync<EnumSeedDocument>(ResourceName, cancellationToken);

        await EnumTranslationSeeding.ApplyAsync(dbContext, document.Enums, cancellationToken);

        // Рахуємо в змінну, а не прямо в аргументі: аналізатор справедливо
        // не любить обчислень у виклику логера — вони виконуються навіть
        // тоді, коли цей рівень логування вимкнено.
        var values = document.Enums.Values.Sum(item => item.Count);

        LogSeeded(logger, values);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Довідники модерації: {Values} значень")]
    private static partial void LogSeeded(ILogger logger, int values);
}
