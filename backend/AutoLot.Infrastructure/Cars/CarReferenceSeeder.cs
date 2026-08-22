using System.Text.Json;
using AutoLot.Application.Common.Abstractions;
using AutoLot.Domain.Cars;
using AutoLot.Domain.Common;
using AutoLot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoLot.Infrastructure.Cars;

/// <summary>
/// Наповнює довідники автомобіля з двох вбудованих файлів: назви значень
/// перелічень і дерево марка → модель → покоління. Як і сідер географії,
/// звіряється за сталим ключем, тож повторний запуск лише оновлює наявне.
/// </summary>
public sealed partial class CarReferenceSeeder(
    AutoLotDbContext dbContext,
    ILogger<CarReferenceSeeder> logger) : IDataSeeder
{
    private const string AttributesResource = "AutoLot.Infrastructure.Persistence.SeedData.car-attributes.json";
    private const string MakesResource = "AutoLot.Infrastructure.Persistence.SeedData.car-makes.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>Після географії — залежності між ними немає, порядок лише для передбачуваності логів.</summary>
    public int Order => 3;

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await SeedAttributesAsync(cancellationToken);
        await SeedMakesAsync(cancellationToken);
    }

    private async Task SeedAttributesAsync(CancellationToken cancellationToken)
    {
        var document = await ReadAsync<CarAttributesSeedDocument>(AttributesResource, cancellationToken);

        var existing = await dbContext.EnumTranslations
            .ToDictionaryAsync(
                translation => (translation.EnumName, translation.Value, translation.Language),
                cancellationToken);

        foreach (var (enumName, values) in document.Enums)
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

    private async Task SeedMakesAsync(CancellationToken cancellationToken)
    {
        var document = await ReadAsync<CarMakesSeedDocument>(MakesResource, cancellationToken);

        var makes = await dbContext.Makes.ToDictionaryAsync(make => make.Slug, cancellationToken);
        var models = await dbContext.Models.ToDictionaryAsync(model => model.Slug, cancellationToken);
        var generations = await dbContext.Generations
            .ToDictionaryAsync(generation => generation.Slug, cancellationToken);

        foreach (var makeSeed in document.Makes)
        {
            if (!makes.TryGetValue(makeSeed.Slug, out var make))
            {
                make = new Make { Slug = makeSeed.Slug };
                dbContext.Makes.Add(make);
                makes[makeSeed.Slug] = make;
            }

            make.Name = makeSeed.Name;
            make.IsPopular = makeSeed.IsPopular;

            foreach (var modelSeed in makeSeed.Models)
            {
                // Slug моделі глобально унікальний, бо містить марку: «audi-a4».
                var modelSlug = $"{makeSeed.Slug}-{modelSeed.Slug}";

                if (!models.TryGetValue(modelSlug, out var model))
                {
                    model = new Model { Slug = modelSlug, Make = make };
                    dbContext.Models.Add(model);
                    models[modelSlug] = model;
                }

                model.Name = modelSeed.Name;

                foreach (var generationSeed in modelSeed.Generations)
                {
                    var generationSlug = $"{modelSlug}-{generationSeed.Slug}";

                    if (!generations.TryGetValue(generationSlug, out var generation))
                    {
                        generation = new Generation { Slug = generationSlug, Model = model };
                        dbContext.Generations.Add(generation);
                        generations[generationSlug] = generation;
                    }

                    generation.Name = generationSeed.Name;
                    generation.YearFrom = generationSeed.YearFrom;
                    generation.YearTo = generationSeed.YearTo;
                }
            }
        }

        var changed = await dbContext.SaveChangesAsync(cancellationToken);

        LogSeeded(logger, makes.Count, models.Count, generations.Count, changed);
    }

    private static async Task<TDocument> ReadAsync<TDocument>(
        string resourceName,
        CancellationToken cancellationToken)
        where TDocument : new()
    {
        await using var stream = typeof(CarReferenceSeeder).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Вбудований ресурс '{resourceName}' не знайдено. Перевірте, що файл додано як EmbeddedResource.");

        return await JsonSerializer.DeserializeAsync<TDocument>(stream, SerializerOptions, cancellationToken)
            ?? new TDocument();
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Довідники авто: {Makes} марок, {Models} моделей, {Generations} поколінь; змінено рядків: {Changed}")]
    private static partial void LogSeeded(
        ILogger logger,
        int makes,
        int models,
        int generations,
        int changed);
}
