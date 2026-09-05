using AutoLot.Application.Analytics;
using AutoLot.Application.Analytics.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutoLot.Api.Controllers;

/// <summary>
/// Ринкові ціни.
///
/// Відкрито всім, зокрема гостю: знання про те, дешевше це авто за ринок чи
/// дорожче, потрібне саме тому, хто ще нічого не вирішив і не реєструвався.
/// Таємниці тут немає — цифри рахуються з оголошень, які й так публічні.
/// </summary>
[ApiController]
[Route("api/analytics")]
[AllowAnonymous]
public sealed class AnalyticsController(IPriceAnalyticsService analytics) : ControllerBase
{
    /// <summary>
    /// Ціни по моделі. 204, якщо оголошень надто мало, щоб щось виводити —
    /// це не помилка, а чесна відповідь «сказати нічого».
    /// </summary>
    [HttpGet("price")]
    [ProducesResponseType<PriceStats>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> GetForModel(
        [FromQuery] long modelId,
        [FromQuery] int? year,
        CancellationToken cancellationToken)
    {
        var stats = await analytics.ForModelAsync(modelId, year, cancellationToken);

        return stats is null ? NoContent() : Ok(stats);
    }

    /// <summary>Ціна конкретного оголошення на тлі ринку.</summary>
    [HttpGet("listings/{listingId:long}/price")]
    [ProducesResponseType<PriceInsight>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> GetForListing(
        long listingId,
        CancellationToken cancellationToken)
    {
        var insight = await analytics.ForListingAsync(listingId, cancellationToken);

        return insight is null ? NoContent() : Ok(insight);
    }
}
