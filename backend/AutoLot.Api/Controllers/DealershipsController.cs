using AutoLot.Application.Common.Abstractions;
using AutoLot.Application.Dealers;
using AutoLot.Application.Dealers.Dtos;
using AutoLot.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutoLot.Api.Controllers;

/// <summary>
/// Автосалони. Картка салону відкрита всім — це вітрина; усе, що стосується
/// персоналу, доступне лише тим, хто в салоні працює.
/// </summary>
[ApiController]
[Route("api/dealerships")]
public sealed class DealershipsController(
    IDealershipService dealerships,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Каталог салонів. Перевірені першими, далі — за наповненістю вітрини.</summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType<IReadOnlyList<DealershipCard>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Search(
        [FromQuery] string? text,
        [FromQuery] long? cityId,
        [FromQuery] bool verifiedOnly = false,
        CancellationToken cancellationToken = default)
    {
        return Ok(await dealerships.SearchAsync(text, cityId, verifiedOnly, cancellationToken));
    }

    [HttpGet("{slug}")]
    [AllowAnonymous]
    [ProducesResponseType<DealershipDetails>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBySlug(string slug, CancellationToken cancellationToken)
    {
        var dealership = await dealerships.GetBySlugAsync(slug, cancellationToken);

        return dealership is null ? NotFound() : Ok(dealership);
    }

    /// <summary>Салони, де працює той, хто питає. Потрібно, щоб намалювати перемикач.</summary>
    [HttpGet("mine")]
    [Authorize]
    [ProducesResponseType<IReadOnlyList<DealershipMembership>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
        {
            return Unauthorized();
        }

        return Ok(await dealerships.GetMembershipsAsync(userId, cancellationToken));
    }

    [HttpPost]
    [Authorize]
    [ProducesResponseType<DealershipDetails>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create(
        CreateDealershipRequest request,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } founderId)
        {
            return Unauthorized();
        }

        var created = await dealerships.CreateAsync(founderId, request, cancellationToken);

        return Created($"/api/dealerships/{created.Slug}", created);
    }

    [HttpGet("{dealershipId:long}/staff")]
    [Authorize]
    [ProducesResponseType<IReadOnlyList<StaffMember>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetStaff(long dealershipId, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } actorId)
        {
            return Unauthorized();
        }

        return Ok(await dealerships.GetStaffAsync(dealershipId, actorId, cancellationToken));
    }

    [HttpPost("{dealershipId:long}/staff")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AddStaff(
        long dealershipId,
        AddStaffRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (currentUser.Id is not { } actorId)
        {
            return Unauthorized();
        }

        await dealerships.AddStaffAsync(
            dealershipId,
            actorId,
            request.Email,
            request.Role,
            cancellationToken);

        return NoContent();
    }

    [HttpDelete("{dealershipId:long}/staff/{userId:long}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RemoveStaff(
        long dealershipId,
        long userId,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } actorId)
        {
            return Unauthorized();
        }

        await dealerships.RemoveStaffAsync(dealershipId, actorId, userId, cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Верифікація салону. Окремо від решти, бо це дія майданчика, а не
    /// салону: бейдж перевіреного ставить модератор.
    /// </summary>
    [HttpPut("{dealershipId:long}/verification")]
    [Authorize(Roles = $"{RoleNames.Moderator},{RoleNames.Admin}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetVerification(
        long dealershipId,
        [FromQuery] bool verified,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } moderatorId)
        {
            return Unauthorized();
        }

        await dealerships.SetVerificationAsync(
            dealershipId,
            moderatorId,
            verified,
            cancellationToken);

        return NoContent();
    }
}
