using AutoLot.Application.Geo;
using AutoLot.Application.Geo.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutoLot.Api.Controllers;

/// <summary>
/// Довідник географії для каскадних списків: область → район області →
/// місто → район міста. Назви приходять мовою із заголовка Accept-Language.
/// Дані публічні, тож автентифікація не потрібна.
/// </summary>
[ApiController]
[Route("api/geo")]
[AllowAnonymous]
public sealed class GeoController(IGeoCatalog geoCatalog) : ControllerBase
{
    /// <summary>Усі області, АР Крим і міста зі спеціальним статусом.</summary>
    [HttpGet("regions")]
    [ProducesResponseType<IReadOnlyList<GeoItem>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRegions(CancellationToken cancellationToken)
    {
        return Ok(await geoCatalog.GetRegionsAsync(cancellationToken));
    }

    /// <summary>
    /// Адміністративні райони області. Порожній список — нормальна відповідь:
    /// у Києва та Севастополя районів області немає взагалі.
    /// </summary>
    [HttpGet("regions/{regionId:long}/districts")]
    [ProducesResponseType<IReadOnlyList<GeoItem>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDistricts(long regionId, CancellationToken cancellationToken)
    {
        return Ok(await geoCatalog.GetDistrictsAsync(regionId, cancellationToken));
    }

    /// <summary>Міста області; за потреби звужені до одного району.</summary>
    [HttpGet("regions/{regionId:long}/cities")]
    [ProducesResponseType<IReadOnlyList<CityItem>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCities(
        long regionId,
        [FromQuery] long? districtId,
        CancellationToken cancellationToken)
    {
        return Ok(await geoCatalog.GetCitiesAsync(regionId, districtId, cancellationToken));
    }

    /// <summary>Райони всередині міста. Є лише у великих містах.</summary>
    [HttpGet("cities/{cityId:long}/districts")]
    [ProducesResponseType<IReadOnlyList<GeoItem>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCityDistricts(long cityId, CancellationToken cancellationToken)
    {
        return Ok(await geoCatalog.GetCityDistrictsAsync(cityId, cancellationToken));
    }
}
