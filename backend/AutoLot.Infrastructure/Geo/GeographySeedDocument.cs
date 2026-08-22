using System.Text.Json.Serialization;

namespace AutoLot.Infrastructure.Geo;

/// <summary>
/// Форма файла geography.json. Це не сутності бази, а лише проміжні об'єкти,
/// у які System.Text.Json розкладає вміст файла — далі сідер переносить їх у
/// справжні таблиці.
/// </summary>
internal sealed record GeographySeedDocument
{
    [JsonPropertyName("regions")]
    public IReadOnlyList<RegionSeed> Regions { get; init; } = [];
}

internal sealed record RegionSeed
{
    public string Code { get; init; } = string.Empty;

    public int SortOrder { get; init; }

    /// <summary>Назви за кодом мови: {"uk": "Київська область", "en": "Kyiv Oblast"}.</summary>
    public Dictionary<string, string> Names { get; init; } = [];

    public IReadOnlyList<DistrictSeed> Districts { get; init; } = [];

    public IReadOnlyList<CitySeed> Cities { get; init; } = [];
}

internal sealed record DistrictSeed
{
    public string Code { get; init; } = string.Empty;

    public Dictionary<string, string> Names { get; init; } = [];
}

internal sealed record CitySeed
{
    public string Code { get; init; } = string.Empty;

    /// <summary>Порожній у міст, не підпорядкованих районові.</summary>
    public string? DistrictCode { get; init; }

    public bool IsRegionCentre { get; init; }

    public int Population { get; init; }

    public Dictionary<string, string> Names { get; init; } = [];

    public IReadOnlyList<CityDistrictSeed> CityDistricts { get; init; } = [];
}

internal sealed record CityDistrictSeed
{
    public string Code { get; init; } = string.Empty;

    public Dictionary<string, string> Names { get; init; } = [];
}

/// <summary>Форма файла countries.json.</summary>
internal sealed record CountriesSeedDocument
{
    public IReadOnlyList<CountrySeed> Countries { get; init; } = [];
}

internal sealed record CountrySeed
{
    public string Code { get; init; } = string.Empty;

    public Dictionary<string, string> Names { get; init; } = [];
}
