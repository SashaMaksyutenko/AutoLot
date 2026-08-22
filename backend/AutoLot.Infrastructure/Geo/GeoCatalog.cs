using AutoLot.Application.Common.Abstractions;
using AutoLot.Application.Geo;
using AutoLot.Application.Geo.Dtos;
using AutoLot.Domain.Common;
using AutoLot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AutoLot.Infrastructure.Geo;

internal sealed class GeoCatalog(AutoLotDbContext dbContext, ICurrentLanguage language) : IGeoCatalog
{
    public async Task<IReadOnlyList<GeoItem>> GetRegionsAsync(CancellationToken cancellationToken = default)
    {
        var code = language.Code;

        return await dbContext.Regions
            .AsNoTracking()
            .Select(region => new
            {
                region.Id,
                region.SortOrder,
                // COALESCE у три кроки: потрібна мова, потім українська,
                // потім код — щоб список ніколи не показував порожній рядок.
                Name = region.Translations.Where(t => t.Language == code).Select(t => t.Name).FirstOrDefault()
                    ?? region.Translations.Where(t => t.Language == LanguageCodes.Default).Select(t => t.Name).FirstOrDefault()
                    ?? region.Code,
            })
            .OrderBy(region => region.SortOrder)
            .ThenBy(region => region.Name)
            .Select(region => new GeoItem(region.Id, region.Name))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<GeoItem>> GetDistrictsAsync(
        long regionId,
        CancellationToken cancellationToken = default)
    {
        var code = language.Code;

        return await dbContext.Districts
            .AsNoTracking()
            .Where(district => district.RegionId == regionId)
            // Спершу анонімний тип, і лише потім GeoItem: сортувати за полем
            // готового запису EF не вміє — не може зібрати його в ORDER BY.
            .Select(district => new
            {
                district.Id,
                Name = district.Translations.Where(t => t.Language == code).Select(t => t.Name).FirstOrDefault()
                    ?? district.Translations.Where(t => t.Language == LanguageCodes.Default).Select(t => t.Name).FirstOrDefault()
                    ?? district.Code,
            })
            .OrderBy(district => district.Name)
            .Select(district => new GeoItem(district.Id, district.Name))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CityItem>> GetCitiesAsync(
        long regionId,
        long? districtId,
        CancellationToken cancellationToken = default)
    {
        var code = language.Code;

        var query = dbContext.Cities
            .AsNoTracking()
            .Where(city => city.RegionId == regionId);

        if (districtId is { } district)
        {
            query = query.Where(city => city.DistrictId == district);
        }

        return await query
            // Сортуємо до проєкції, поки ще видно населення: обласний центр
            // першим, далі за спаданням розміру, а за рівних — за абеткою.
            // Так людина майже завжди бачить своє місто вгорі списку.
            .OrderByDescending(city => city.IsRegionCentre)
            .ThenByDescending(city => city.Population)
            .ThenBy(city => city.Translations
                .Where(t => t.Language == LanguageCodes.Default)
                .Select(t => t.Name)
                .FirstOrDefault())
            .Select(city => new CityItem(
                city.Id,
                city.Translations.Where(t => t.Language == code).Select(t => t.Name).FirstOrDefault()
                    ?? city.Translations.Where(t => t.Language == LanguageCodes.Default).Select(t => t.Name).FirstOrDefault()
                    ?? city.Code,
                city.RegionId,
                city.DistrictId,
                city.IsRegionCentre,
                city.CityDistricts.Count > 0))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<GeoItem>> GetCityDistrictsAsync(
        long cityId,
        CancellationToken cancellationToken = default)
    {
        var code = language.Code;

        return await dbContext.CityDistricts
            .AsNoTracking()
            .Where(district => district.CityId == cityId)
            .Select(district => new
            {
                district.Id,
                Name = district.Translations.Where(t => t.Language == code).Select(t => t.Name).FirstOrDefault()
                    ?? district.Translations.Where(t => t.Language == LanguageCodes.Default).Select(t => t.Name).FirstOrDefault()
                    ?? district.Code,
            })
            .OrderBy(district => district.Name)
            .Select(district => new GeoItem(district.Id, district.Name))
            .ToListAsync(cancellationToken);
    }

    public async Task<UserLocation?> GetLocationAsync(
        long cityId,
        long? cityDistrictId,
        CancellationToken cancellationToken = default)
    {
        var code = language.Code;

        var location = await dbContext.Cities
            .AsNoTracking()
            .Where(city => city.Id == cityId)
            .Select(city => new
            {
                CityId = city.Id,
                CityName = city.Translations.Where(t => t.Language == code).Select(t => t.Name).FirstOrDefault()
                    ?? city.Translations.Where(t => t.Language == LanguageCodes.Default).Select(t => t.Name).FirstOrDefault()
                    ?? city.Code,
                city.RegionId,
                RegionName = city.Region.Translations.Where(t => t.Language == code).Select(t => t.Name).FirstOrDefault()
                    ?? city.Region.Translations.Where(t => t.Language == LanguageCodes.Default).Select(t => t.Name).FirstOrDefault()
                    ?? city.Region.Code,
                city.DistrictId,
                DistrictName = city.District == null
                    ? null
                    : city.District.Translations.Where(t => t.Language == code).Select(t => t.Name).FirstOrDefault()
                        ?? city.District.Translations.Where(t => t.Language == LanguageCodes.Default).Select(t => t.Name).FirstOrDefault(),
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (location is null)
        {
            return null;
        }

        string? cityDistrictName = null;

        if (cityDistrictId is { } districtId)
        {
            cityDistrictName = await dbContext.CityDistricts
                .AsNoTracking()
                .Where(district => district.Id == districtId && district.CityId == cityId)
                .Select(district =>
                    district.Translations.Where(t => t.Language == code).Select(t => t.Name).FirstOrDefault()
                    ?? district.Translations.Where(t => t.Language == LanguageCodes.Default).Select(t => t.Name).FirstOrDefault()
                    ?? district.Code)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return new UserLocation(
            location.RegionId,
            location.RegionName,
            location.DistrictId,
            location.DistrictName,
            location.CityId,
            location.CityName,
            cityDistrictName is null ? null : cityDistrictId,
            cityDistrictName);
    }

    public async Task<IReadOnlyList<GeoItem>> GetCountriesAsync(
        CancellationToken cancellationToken = default)
    {
        var code = language.Code;

        return await dbContext.Countries
            .AsNoTracking()
            .Select(country => new
            {
                country.Id,
                country.SortOrder,
                Name = country.Translations.Where(t => t.Language == code).Select(t => t.Name).FirstOrDefault()
                    ?? country.Translations.Where(t => t.Language == LanguageCodes.Default).Select(t => t.Name).FirstOrDefault()
                    ?? country.Code,
            })
            .OrderBy(country => country.SortOrder)
            .ThenBy(country => country.Name)
            .Select(country => new GeoItem(country.Id, country.Name))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> LocationExistsAsync(
        long cityId,
        long? cityDistrictId,
        CancellationToken cancellationToken = default)
    {
        if (!await dbContext.Cities.AnyAsync(city => city.Id == cityId, cancellationToken))
        {
            return false;
        }

        if (cityDistrictId is not { } districtId)
        {
            return true;
        }

        // Важлива саме перевірка належності: інакше можна було б підсунути
        // Оболонський район до Львова.
        return await dbContext.CityDistricts
            .AnyAsync(district => district.Id == districtId && district.CityId == cityId, cancellationToken);
    }
}
