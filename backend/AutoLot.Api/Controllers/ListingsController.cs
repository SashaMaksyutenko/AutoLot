using AutoLot.Application.Common.Abstractions;
using AutoLot.Application.Listings;
using AutoLot.Application.Listings.Dtos;
using AutoLot.Domain.Enums;
using AutoLot.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutoLot.Api.Controllers;

/// <summary>
/// Робота автора зі своїми оголошеннями. Ідентифікатор продавця скрізь
/// береться з токена, а не з тіла запиту — інакше можна було б створити
/// оголошення від чужого імені.
/// </summary>
[ApiController]
[Route("api/listings")]
[Authorize]
public sealed class ListingsController(
    IListingService listingService,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Створює чернетку. У видачу вона потрапить лише після модерації.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        CreateListingRequest request,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } sellerId)
        {
            return Unauthorized();
        }

        var listingId = await listingService.CreateAsync(sellerId, request, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { listingId }, new { id = listingId });
    }

    [HttpPut("{listingId:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        long listingId,
        UpdateListingRequest request,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } actorId)
        {
            return Unauthorized();
        }

        await listingService.UpdateAsync(listingId, actorId, request, cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Картка оголошення. Доступна без входу, але лише для опублікованих;
    /// чужу чернетку метод не покаже й не підтвердить її існування.
    /// </summary>
    [HttpGet("{listingId:long}")]
    [AllowAnonymous]
    [ProducesResponseType<ListingDetails>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(long listingId, CancellationToken cancellationToken)
    {
        var details = await listingService.GetAsync(
            listingId,
            currentUser.Id,
            IsModerator(),
            cancellationToken);

        return details is null ? NotFound() : Ok(details);
    }

    /// <summary>Власні оголошення, за потреби відфільтровані за статусом.</summary>
    [HttpGet("mine")]
    [ProducesResponseType<IReadOnlyList<ListingSummary>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMine(
        [FromQuery] ListingStatus? status,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } sellerId)
        {
            return Unauthorized();
        }

        return Ok(await listingService.GetOwnAsync(sellerId, status, cancellationToken));
    }

    /// <summary>Подає оголошення на розгляд модератора.</summary>
    [HttpPost("{listingId:long}/submit")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Submit(long listingId, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } actorId)
        {
            return Unauthorized();
        }

        await listingService.SubmitForModerationAsync(listingId, actorId, cancellationToken);

        return NoContent();
    }

    /// <summary>Кому можна приписати угоду — ті, хто писав про це авто.</summary>
    [HttpGet("{listingId:long}/buyer-candidates")]
    [ProducesResponseType<IReadOnlyList<BuyerCandidate>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBuyerCandidates(
        long listingId,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } actorId)
        {
            return Unauthorized();
        }

        return Ok(await listingService.GetBuyerCandidatesAsync(listingId, actorId, cancellationToken));
    }

    [HttpPost("{listingId:long}/sold")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> MarkSold(
        long listingId,
        MarkSoldRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (currentUser.Id is not { } actorId)
        {
            return Unauthorized();
        }

        await listingService.MarkSoldAsync(listingId, actorId, request.BuyerId, cancellationToken);

        return NoContent();
    }

    [HttpPost("{listingId:long}/archive")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Archive(long listingId, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } actorId)
        {
            return Unauthorized();
        }

        await listingService.ArchiveAsync(listingId, actorId, cancellationToken);

        return NoContent();
    }

    /// <summary>Видаляє чернетку назавжди. Опубліковане оголошення архівують.</summary>
    [HttpDelete("{listingId:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(long listingId, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } actorId)
        {
            return Unauthorized();
        }

        await listingService.DeleteDraftAsync(listingId, actorId, cancellationToken);

        return NoContent();
    }

    private bool IsModerator() =>
        currentUser.IsInRole(RoleNames.Moderator) || currentUser.IsInRole(RoleNames.Admin);
}
