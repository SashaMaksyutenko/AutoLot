using AutoLot.Application.Common.Abstractions;
using AutoLot.Domain.Cars;
using AutoLot.Domain.Enums;
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
    private const string FeaturesResource = "AutoLot.Infrastructure.Persistence.SeedData.car-features.json";


    /// <summary>Після географії — залежності між ними немає, порядок лише для передбачуваності логів.</summary>
    public int Order => 3;

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await SeedAttributesAsync(cancellationToken);
        await SeedFeaturesAsync(cancellationToken);
        await SeedMakesAsync(cancellationToken);
    }

    /// <summary>
    /// Опції комплектації. Порядок у файлі стає порядком у формі, а категорія
    /// розбирається з рядка — якщо у файлі трапиться невідома, сід впаде одразу
    /// й голосно, а не створить опцію без розділу.
    /// </summary>
    private async Task SeedFeaturesAsync(CancellationToken cancellationToken)
    {
        var document = await SeedResource.ReadAsync<CarFeaturesSeedDocument>(FeaturesResource, cancellationToken);

        var features = await dbContext.Features
            .Include(feature => feature.Translations)
            .ToDictionaryAsync(feature => feature.Code, cancellationToken);

        for (var sortOrder = 0; sortOrder < document.Features.Count; sortOrder++)
        {
            var seed = document.Features[sortOrder];

            if (!features.TryGetValue(seed.Code, out var feature))
            {
                feature = new Feature { Code = seed.Code };
                dbContext.Features.Add(feature);
                features[seed.Code] = feature;
            }

            feature.Category = Enum.Parse<FeatureCategory>(seed.Category);
            feature.SortOrder = sortOrder;

            TranslationSeeding.Apply(feature.Translations, seed.Names, () => new FeatureTranslation());
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        LogFeaturesSeeded(logger, features.Count);
    }

    private async Task SeedAttributesAsync(CancellationToken cancellationToken)
    {
        var document = await SeedResource.ReadAsync<CarAttributesSeedDocument>(AttributesResource, cancellationToken);

        await EnumTranslationSeeding.ApplyAsync(dbContext, document.Enums, cancellationToken);
    }

    private async Task SeedMakesAsync(CancellationToken cancellationToken)
    {
        var document = await SeedResource.ReadAsync<CarMakesSeedDocument>(MakesResource, cancellationToken);

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


    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Довідники авто: {Makes} марок, {Models} моделей, {Generations} поколінь; змінено рядків: {Changed}")]
    private static partial void LogSeeded(
        ILogger logger,
        int makes,
        int models,
        int generations,
        int changed);

    [LoggerMessage(Level = LogLevel.Information, Message = "Опцій комплектації: {Features}")]
    private static partial void LogFeaturesSeeded(ILogger logger, int features);
}
