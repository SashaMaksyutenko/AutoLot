namespace AutoLot.Application.Geo.Dtos;

public sealed record CityItem(
    long Id,
    string Name,
    long RegionId,
    long? DistrictId,
    bool IsRegionCentre,
    /// <summary>Чи є сенс показувати наступний рівень — список районів міста.</summary>
    bool HasCityDistricts);
