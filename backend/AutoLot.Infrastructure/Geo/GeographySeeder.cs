using AutoLot.Application.Common.Abstractions;
using AutoLot.Domain.Common;
using AutoLot.Domain.Geo;
using AutoLot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoLot.Infrastructure.Geo;

/// <summary>
/// Наповнює довідник географії з вбудованого файла geography.json.
/// Порівнює записи за сталим кодом, тож повторний запуск оновлює наявні рядки,
/// а не створює копії; додати нові області чи міста можна просто дописавши їх
/// у файл — код міняти не доведеться.
/// </summary>
public sealed partial class GeographySeeder(
    AutoLotDbContext dbContext,
    ILogger<GeographySeeder> logger) : IDataSeeder
{
    private const string ResourceName = "AutoLot.Infrastructure.Persistence.SeedData.geography.json";
    private const string CountriesResourceName = "AutoLot.Infrastructure.Persistence.SeedData.countries.json";


    /// <summary>Географія не залежить ні від чого, тож іде першою.</summary>
    public int Order => 1;

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var document = await ReadDocumentAsync(cancellationToken);

        if (document.Regions.Count == 0)
        {
            LogEmptyDocument(logger);
            return;
        }

        // Тягнемо наявні дані одним запитом на таблицю й далі працюємо в пам'яті:
        // інакше на кожен із сотень записів пішов би окремий SELECT.
        var regions = await dbContext.Regions
            .Include(region => region.Translations)
            .ToDictionaryAsync(region => region.Code, cancellationToken);

        var districts = await dbContext.Districts
            .Include(district => district.Translations)
            .ToDictionaryAsync(district => district.Code, cancellationToken);

        var cities = await dbContext.Cities
            .Include(city => city.Translations)
            .ToDictionaryAsync(city => city.Code, cancellationToken);

        var cityDistricts = await dbContext.CityDistricts
            .Include(district => district.Translations)
            .ToDictionaryAsync(district => district.Code, cancellationToken);

        foreach (var regionSeed in document.Regions)
        {
            var region = Upsert(regions, regionSeed.Code, () => new Region { Code = regionSeed.Code });
            region.SortOrder = regionSeed.SortOrder;
            TranslationSeeding.Apply(region.Translations, regionSeed.Names, () => new RegionTranslation());

            foreach (var districtSeed in regionSeed.Districts)
            {
                var district = Upsert(
                    districts,
                    districtSeed.Code,
                    () => new District { Code = districtSeed.Code, Region = region });

                TranslationSeeding.Apply(district.Translations, districtSeed.Names, () => new DistrictTranslation());
            }

            foreach (var citySeed in regionSeed.Cities)
            {
                var city = Upsert(cities, citySeed.Code, () => new City { Code = citySeed.Code, Region = region });

                city.IsRegionCentre = citySeed.IsRegionCentre;
                city.Population = citySeed.Population;

                // Прив'язку до району ставимо через навігацію, а не через
                // DistrictId: у щойно створеного району ключа ще немає, його
                // видасть база під час збереження, і EF підставить сам.
                city.District = citySeed.DistrictCode is { Length: > 0 } districtCode
                    && districts.TryGetValue(districtCode, out var cityDistrictOwner)
                        ? cityDistrictOwner
                        : null;

                TranslationSeeding.Apply(city.Translations, citySeed.Names, () => new CityTranslation());

                foreach (var cityDistrictSeed in citySeed.CityDistricts)
                {
                    var cityDistrict = Upsert(
                        cityDistricts,
                        cityDistrictSeed.Code,
                        () => new CityDistrict { Code = cityDistrictSeed.Code, City = city });

                    TranslationSeeding.Apply(
                        cityDistrict.Translations,
                        cityDistrictSeed.Names,
                        () => new CityDistrictTranslation());
                }
            }
        }

        var changed = await dbContext.SaveChangesAsync(cancellationToken);

        LogSeeded(logger, regions.Count, districts.Count, cities.Count, cityDistricts.Count, changed);

        await SeedCountriesAsync(cancellationToken);
    }

    /// <summary>
    /// Повертає наявний запис за кодом або створює новий і одразу кладе його
    /// і в контекст, і в довідник — щоб наступні звертання його вже бачили.
    /// </summary>
    private TEntity Upsert<TEntity>(Dictionary<string, TEntity> known, string code, Func<TEntity> create)
        where TEntity : class
    {
        if (known.TryGetValue(code, out var existing))
        {
            return existing;
        }

        var created = create();
        dbContext.Add(created);
        known[code] = created;

        return created;
    }

    /// <summary>
    /// Країни для полів «країна-виробник» і «країна пригону». До ієрархії
    /// областей стосунку не мають, але це теж географія, тож живуть тут.
    /// </summary>
    private async Task SeedCountriesAsync(CancellationToken cancellationToken)
    {
        var document = await SeedResource.ReadAsync<CountriesSeedDocument>(CountriesResourceName, cancellationToken);

        var countries = await dbContext.Countries
            .Include(country => country.Translations)
            .ToDictionaryAsync(country => country.Code, cancellationToken);

        for (var sortOrder = 0; sortOrder < document.Countries.Count; sortOrder++)
        {
            var seed = document.Countries[sortOrder];
            var country = Upsert(countries, seed.Code, () => new Country { Code = seed.Code });

            country.SortOrder = sortOrder;
            TranslationSeeding.Apply(country.Translations, seed.Names, () => new CountryTranslation());
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        LogCountriesSeeded(logger, countries.Count);
    }

    private static Task<GeographySeedDocument> ReadDocumentAsync(CancellationToken cancellationToken) =>
        SeedResource.ReadAsync<GeographySeedDocument>(ResourceName, cancellationToken);


    [LoggerMessage(Level = LogLevel.Information, Message = "Країн у довіднику: {Countries}")]
    private static partial void LogCountriesSeeded(ILogger logger, int countries);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Файл географії порожній — довідник не наповнено")]
    private static partial void LogEmptyDocument(ILogger logger);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Географія: {Regions} областей, {Districts} районів, {Cities} міст, {CityDistricts} районів міст; змінено рядків: {Changed}")]
    private static partial void LogSeeded(
        ILogger logger,
        int regions,
        int districts,
        int cities,
        int cityDistricts,
        int changed);
}
