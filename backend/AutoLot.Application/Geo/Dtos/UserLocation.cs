namespace AutoLot.Application.Geo.Dtos;

/// <summary>
/// Розгорнуте місцезнаходження для показу. У базі в користувача лежить лише
/// місто й, за потреби, район міста — решту рівнів дістаємо звідти.
/// </summary>
public sealed record UserLocation(
    long RegionId,
    string RegionName,
    long? DistrictId,
    string? DistrictName,
    long CityId,
    string CityName,
    long? CityDistrictId,
    string? CityDistrictName);
