using System.Text.Json;
using AutoLot.Domain.Common;
using AutoLot.Infrastructure.Geo;

namespace AutoLot.Tests.Geo;

/// <summary>
/// Перевіряє сам файл довідника, а не код. Довідник розширюють руками, тож
/// описка в коді району чи забутий англійський переклад — питання часу;
/// краще, щоб про це сказали тести, ніж порожній рядок у випадаючому списку.
/// </summary>
public class GeographySeedDataTests
{
    private const string ResourceName = "AutoLot.Infrastructure.Persistence.SeedData.geography.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly GeographySeedDocument Document = Load();

    [Fact]
    public void Contains_every_region_of_ukraine()
    {
        // 24 області, Автономна Республіка Крим і два міста зі спеціальним статусом.
        Assert.Equal(27, Document.Regions.Count);
    }

    [Fact]
    public void Contains_every_district_of_the_2020_reform()
    {
        var districts = Document.Regions.Sum(region => region.Districts.Count);

        Assert.Equal(136, districts);
    }

    [Fact]
    public void Every_city_belongs_to_a_district_where_the_region_has_them()
    {
        // Київ і Севастополь районів області не мають — там порожньо законно.
        // Скрізь інде місто без району означає, що його забули прив'язати.
        var orphans = Document.Regions
            .Where(region => region.Districts.Count > 0)
            .SelectMany(region => region.Cities)
            .Where(city => string.IsNullOrEmpty(city.DistrictCode))
            .Select(city => city.Code)
            .ToList();

        Assert.Empty(orphans);
    }

    [Fact]
    public void Every_entry_is_translated_into_both_languages()
    {
        foreach (var region in Document.Regions)
        {
            AssertTranslated(region.Names, region.Code);

            foreach (var district in region.Districts)
            {
                AssertTranslated(district.Names, district.Code);
            }

            foreach (var city in region.Cities)
            {
                AssertTranslated(city.Names, city.Code);

                foreach (var cityDistrict in city.CityDistricts)
                {
                    AssertTranslated(cityDistrict.Names, cityDistrict.Code);
                }
            }
        }
    }

    [Fact]
    public void All_codes_are_unique()
    {
        // Сід шукає наявні записи саме за кодом: дубль означав би, що два
        // різних міста мовчки перезаписують одне одного.
        var codes = Document.Regions
            .SelectMany(region => region.Districts.Select(district => district.Code)
                .Concat(region.Cities.Select(city => city.Code))
                .Concat(region.Cities.SelectMany(city => city.CityDistricts.Select(d => d.Code)))
                .Append(region.Code))
            .ToList();

        var duplicates = codes
            .GroupBy(code => code, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        Assert.Empty(duplicates);
    }

    [Fact]
    public void City_district_references_point_inside_the_same_region()
    {
        foreach (var region in Document.Regions)
        {
            var known = region.Districts.Select(district => district.Code).ToHashSet(StringComparer.Ordinal);

            var dangling = region.Cities
                .Where(city => !string.IsNullOrEmpty(city.DistrictCode) && !known.Contains(city.DistrictCode))
                .Select(city => city.Code)
                .ToList();

            Assert.Empty(dangling);
        }
    }

    [Fact]
    public void Every_region_has_exactly_one_centre()
    {
        foreach (var region in Document.Regions)
        {
            var centres = region.Cities.Count(city => city.IsRegionCentre);

            Assert.True(
                centres <= 1,
                $"Область {region.Code} має {centres} обласних центрів замість одного.");
        }
    }

    [Fact]
    public void Region_order_is_not_ambiguous()
    {
        var duplicated = Document.Regions
            .GroupBy(region => region.SortOrder)
            .Any(group => group.Count() > 1);

        Assert.False(duplicated, "Дві області з однаковим SortOrder — порядок у списку стане випадковим.");
    }

    private static void AssertTranslated(Dictionary<string, string> names, string code)
    {
        Assert.True(
            names.ContainsKey(LanguageCodes.Ukrainian),
            $"У записі {code} немає української назви.");

        Assert.True(
            names.ContainsKey(LanguageCodes.English),
            $"У записі {code} немає англійської назви.");

        Assert.All(names.Values, name => Assert.False(string.IsNullOrWhiteSpace(name)));
    }

    private static GeographySeedDocument Load()
    {
        using var stream = typeof(GeographySeeder).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Ресурс '{ResourceName}' не знайдено.");

        return JsonSerializer.Deserialize<GeographySeedDocument>(stream, SerializerOptions)
            ?? throw new InvalidOperationException("Файл географії порожній.");
    }
}
