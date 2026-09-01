using AutoLot.Application.Cars.Dtos;
using AutoLot.Application.Common.Abstractions;
using AutoLot.Application.Listings;
using AutoLot.Application.Listings.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutoLot.Api.Controllers;

/// <summary>
/// Скарги з боку відвідувача.
///
/// Читання чужих скарг тут немає взагалі — і не через недогляд. Скарга
/// адресована модератору, а не публіці: якби її бачив автор оголошення,
/// вона стала б знаряддям тиску, а не сигналом.
/// </summary>
[ApiController]
[Route("api/reports")]
[Authorize]
public sealed class ListingReportsController(
    IListingReportService reports,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>
    /// Причини скарги для випадаючого списку. Відкрито всім: список потрібен
    /// формі ще до того, як гість натисне «увійти», і таємниці в ньому немає.
    /// </summary>
    [HttpGet("reasons")]
    [AllowAnonymous]
    [ProducesResponseType<IReadOnlyList<LookupItem>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReasons(CancellationToken cancellationToken)
    {
        return Ok(await reports.GetReasonsAsync(cancellationToken));
    }

    /// <summary>
    /// Подає скаргу. Повторна скарга на те саме оголошення нової не створює —
    /// у відповіді буде <c>isNew: false</c>.
    /// </summary>
    [HttpPost("listings/{listingId:long}")]
    [ProducesResponseType<ReportReceipt>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Submit(
        long listingId,
        SubmitReportRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (currentUser.Id is not { } reporterId)
        {
            return Unauthorized();
        }

        var receipt = await reports.SubmitAsync(listingId, reporterId, request, cancellationToken);

        return Created($"/api/reports/listings/{listingId}", receipt);
    }
}
