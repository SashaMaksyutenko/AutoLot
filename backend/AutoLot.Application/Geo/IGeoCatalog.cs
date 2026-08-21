using AutoLot.Application.Geo.Dtos;

namespace AutoLot.Application.Geo;

/// <summary>
/// Читання довідника географії. Назви повертаються мовою поточного запиту;
/// якщо перекладу немає, підставляється українська.
/// </summary>
public interface IGeoCatalog
{
    Task<IReadOnlyList<GeoItem>> GetRegionsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GeoItem>> GetDistrictsAsync(
        long regionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Міста області. <paramref name="districtId"/> звужує список до одного
    /// району; без нього повертаються всі міста області.
    /// </summary>
    Task<IReadOnlyList<CityItem>> GetCitiesAsync(
        long regionId,
        long? districtId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GeoItem>> GetCityDistrictsAsync(
        long cityId,
        CancellationToken cancellationToken = default);

    /// <summary>Розгортає збережену в профілі пару «місто + район міста» в повну адресу.</summary>
    Task<UserLocation?> GetLocationAsync(
        long cityId,
        long? cityDistrictId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Чи існує таке місто і чи справді вказаний район належить саме йому.
    /// Ідентифікатори приходять від клієнта, тож на слово їм не віримо (SPEC §8).
    /// </summary>
    Task<bool> LocationExistsAsync(
        long cityId,
        long? cityDistrictId,
        CancellationToken cancellationToken = default);
}
