using AutoLot.Application.Common.Abstractions;
using AutoLot.Application.Listings;
using AutoLot.Application.Listings.Dtos;
using AutoLot.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutoLot.Api.Controllers;

/// <summary>
/// Черга скарг. Окремий контролер від тієї, що подають автори: там роль
/// перевіряється атрибутом на рівні контролера, і тут так само — жоден новий
/// метод не може випадково лишитися відкритим.
/// </summary>
[ApiController]
[Route("api/moderation/reports")]
[Authorize(Roles = $"{RoleNames.Moderator},{RoleNames.Admin}")]
public sealed class ModerationReportsController(
    IListingReportService reports,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Нерозглянуті скарги, найдавніші першими.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ReportSummary>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetQueue(CancellationToken cancellationToken)
    {
        return Ok(await reports.GetQueueAsync(cancellationToken));
    }

    /// <summary>
    /// Рішення модератора. Слушна скарга знімає оголошення з публікації —
    /// автор побачить причину й зможе виправити та подати знову.
    /// </summary>
    [HttpPost("{reportId:long}/resolve")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Resolve(
        long reportId,
        ResolveReportRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (currentUser.Id is not { } moderatorId)
        {
            return Unauthorized();
        }

        await reports.ResolveAsync(reportId, moderatorId, request, cancellationToken);

        return NoContent();
    }
}
