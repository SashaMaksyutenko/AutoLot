using AutoLot.Application.Catalog;
using AutoLot.Application.Common;
using AutoLot.Application.Listings.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutoLot.Api.Controllers;

/// <summary>
/// Публічний каталог оголошень. Показує лише активні — це межа видимості,
/// а не фільтр, який можна зняти параметром.
/// </summary>
[ApiController]
[Route("api/catalog")]
[AllowAnonymous]
public sealed class CatalogController(ICatalogService catalogService) : ControllerBase
{
    /// <summary>
    /// Пошук із фільтрами, сортуванням і пагінацією. Усі параметри
    /// необов'язкові; порожній запит показує все активне, найновіше першим.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<ListingSummary>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Search(
        [FromQuery] CatalogQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await catalogService.SearchAsync(query, cancellationToken));
    }
}
