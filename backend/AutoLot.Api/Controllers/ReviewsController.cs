using AutoLot.Application.Common.Abstractions;
using AutoLot.Application.Listings;
using AutoLot.Application.Listings.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutoLot.Api.Controllers;

/// <summary>
/// Відгуки про угоди.
///
/// Читання відкрите: у публічності вся користь відгуку — покупець має бачити
/// репутацію продавця ДО того, як напише йому. Закриті лише дії.
/// </summary>
[ApiController]
[Route("api")]
public sealed class ReviewsController(
    IReviewService reviews,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Що написано під цією угодою — і чи може той, хто дивиться, додати своє.</summary>
    [HttpGet("listings/{listingId:long}/reviews")]
    [AllowAnonymous]
    [ProducesResponseType<DealReviews>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetForListing(
        long listingId,
        CancellationToken cancellationToken)
    {
        return Ok(await reviews.GetForListingAsync(listingId, currentUser.Id, cancellationToken));
    }

    /// <summary>
    /// Лишає відгук. Про кого він — сервер вирішує сам за складом угоди,
    /// тож приписати відгук сторонньому неможливо навіть підробленим тілом.
    /// </summary>
    [HttpPost("listings/{listingId:long}/reviews")]
    [Authorize]
    [ProducesResponseType<ReviewRecord>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Leave(
        long listingId,
        LeaveReviewRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (currentUser.Id is not { } authorId)
        {
            return Unauthorized();
        }

        var review = await reviews.LeaveAsync(listingId, authorId, request, cancellationToken);

        return Created($"/api/listings/{listingId}/reviews", review);
    }

    /// <summary>Усе, що написали про людину.</summary>
    [HttpGet("users/{userId:long}/reviews")]
    [AllowAnonymous]
    [ProducesResponseType<IReadOnlyList<ReviewRecord>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAbout(long userId, CancellationToken cancellationToken)
    {
        return Ok(await reviews.GetAboutAsync(userId, cancellationToken));
    }

    /// <summary>Рейтинг людини одним рядком — для картки продавця.</summary>
    [HttpGet("users/{userId:long}/rating")]
    [AllowAnonymous]
    [ProducesResponseType<RatingSummary>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRating(long userId, CancellationToken cancellationToken)
    {
        return Ok(await reviews.GetRatingAsync(userId, cancellationToken));
    }
}
