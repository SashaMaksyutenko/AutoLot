using AutoLot.Application.Common.Abstractions;
using AutoLot.Application.Listings;
using AutoLot.Application.Listings.Dtos;
using AutoLot.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutoLot.Api.Controllers;

/// <summary>
/// Черга модерації. Роль перевіряється атрибутом на рівні контролера, тож
/// жоден новий метод не може випадково лишитися відкритим.
/// </summary>
[ApiController]
[Route("api/moderation/listings")]
[Authorize(Roles = $"{RoleNames.Moderator},{RoleNames.Admin}")]
public sealed class ModerationController(
    IModerationService moderationService,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Оголошення, що чекають рішення, найдавніші першими.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ListingSummary>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetQueue(CancellationToken cancellationToken)
    {
        return Ok(await moderationService.GetQueueAsync(cancellationToken));
    }

    [HttpPost("{listingId:long}/approve")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Approve(long listingId, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } moderatorId)
        {
            return Unauthorized();
        }

        await moderationService.ApproveAsync(listingId, moderatorId, cancellationToken);

        return NoContent();
    }

    [HttpPost("{listingId:long}/reject")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reject(
        long listingId,
        RejectListingRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (currentUser.Id is not { } moderatorId)
        {
            return Unauthorized();
        }

        await moderationService.RejectAsync(listingId, moderatorId, request.Reason, cancellationToken);

        return NoContent();
    }
}
