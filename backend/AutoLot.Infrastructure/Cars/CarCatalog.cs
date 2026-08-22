using AutoLot.Application.Cars;
using AutoLot.Application.Cars.Dtos;
using AutoLot.Application.Common.Abstractions;
using AutoLot.Domain.Common;
using AutoLot.Domain.Enums;
using AutoLot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AutoLot.Infrastructure.Cars;

internal sealed class CarCatalog(AutoLotDbContext dbContext, ICurrentLanguage language) : ICarCatalog
{
    public async Task<CarAttributes> GetAttributesAsync(CancellationToken cancellationToken = default)
    {
        var code = language.Code;

        // Усіх значень тут менше сотні на дві мови, тож тягнемо одним запитом
        // і розкладаємо в пам'яті — це дешевше за п'ять окремих звернень.
        var rows = await dbContext.EnumTranslations
            .AsNoTracking()
            .Where(translation => translation.Language == code
                || translation.Language == LanguageCodes.Default)
            .Select(translation => new TranslationRow(
                translation.EnumName,
                translation.Value,
                translation.Language,
                translation.Name,
                translation.SortOrder))
            .ToListAsync(cancellationToken);

        return new CarAttributes(
            Pick(rows, nameof(BodyType), code),
            Pick(rows, nameof(FuelType), code),
            Pick(rows, nameof(TransmissionType), code),
            Pick(rows, nameof(DrivetrainType), code),
            Pick(rows, nameof(CarColor), code));
    }

    public async Task<IReadOnlyList<MakeItem>> GetMakesAsync(
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Makes
            .AsNoTracking()
            // Популярні марки вгорі: їх шукають у переважній більшості випадків.
            .OrderByDescending(make => make.IsPopular)
            .ThenBy(make => make.Name)
            .Select(make => new MakeItem(
                make.Id,
                make.Name,
                make.Slug,
                make.IsPopular,
                make.Models.Count))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ModelItem>> GetModelsAsync(
        long makeId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Models
            .AsNoTracking()
            .Where(model => model.MakeId == makeId)
            .OrderBy(model => model.Name)
            .Select(model => new ModelItem(
                model.Id,
                model.Name,
                model.Slug,
                model.Generations.Count > 0))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<GenerationItem>> GetGenerationsAsync(
        long modelId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Generations
            .AsNoTracking()
            .Where(generation => generation.ModelId == modelId)
            // Найновіше покоління першим — його шукають найчастіше.
            .OrderByDescending(generation => generation.YearFrom)
            .Select(generation => new GenerationItem(
                generation.Id,
                generation.Name,
                generation.Slug,
                generation.YearFrom,
                generation.YearTo))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FeatureGroup>> GetFeaturesAsync(
        CancellationToken cancellationToken = default)
    {
        var code = language.Code;

        var rows = await dbContext.Features
            .AsNoTracking()
            .Select(feature => new
            {
                feature.Id,
                feature.Code,
                feature.Category,
                feature.SortOrder,
                Name = feature.Translations.Where(t => t.Language == code).Select(t => t.Name).FirstOrDefault()
                    ?? feature.Translations.Where(t => t.Language == LanguageCodes.Default).Select(t => t.Name).FirstOrDefault()
                    ?? feature.Code,
            })
            .OrderBy(feature => feature.SortOrder)
            .ToListAsync(cancellationToken);

        // Групування робимо в пам'яті: опцій менше сотні, а SQL-варіант
        // повернув би ті самі рядки, лише складнішим запитом.
        return
        [
            .. rows
                .GroupBy(feature => feature.Category)
                .Select(group => new FeatureGroup(
                    group.Key.ToString(),
                    [.. group.Select(feature => new FeatureItem(feature.Id, feature.Code, feature.Name))])),
        ];
    }

    /// <summary>
    /// Вибирає значення одного перелічення потрібною мовою. Якщо перекладу
    /// немає, лишається той, що знайшовся — тобто українська.
    /// </summary>
    private static IReadOnlyList<LookupItem> Pick(
        List<TranslationRow> rows,
        string enumName,
        string language)
    {
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

    private sealed record TranslationRow(
        string EnumName,
        string Value,
        string Language,
        string Name,
        int SortOrder);
}
