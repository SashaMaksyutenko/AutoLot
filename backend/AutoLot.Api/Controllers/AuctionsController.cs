using AutoLot.Application.Auctions;
using AutoLot.Application.Auctions.Dtos;
using AutoLot.Application.Common.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutoLot.Api.Controllers;

/// <summary>
/// Торги за лотом. Адреса будується навколо оголошення: для відвідувача лот
/// і оголошення — та сама сторінка, і власний номер аукціону йому нічого
/// не сказав би.
/// </summary>
[ApiController]
[Route("api/listings/{listingId:long}/auction")]
public sealed class AuctionsController(
    IAuctionService auctions,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Стан торгів. Дивитися можна без входу — торги публічні.</summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType<AuctionDetails>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(long listingId, CancellationToken cancellationToken)
    {
        var auction = await auctions.GetAsync(listingId, currentUser.Id, cancellationToken);

        return auction is null ? NotFound() : Ok(auction);
    }

    /// <summary>Публічна історія ставок — доказ, що торги справжні (SPEC §4).</summary>
    [HttpGet("bids")]
    [AllowAnonymous]
    [ProducesResponseType<IReadOnlyList<BidRecord>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHistory(long listingId, CancellationToken cancellationToken)
    {
        return Ok(await auctions.GetHistoryAsync(listingId, cancellationToken));
    }

    /// <summary>
    /// Зробити ставку. У тілі — СТЕЛЯ, а не сума до сплати: система поставить
    /// рівно стільки, скільки потрібно для лідерства.
    /// </summary>
    [HttpPost("bids")]
    [Authorize]
    [ProducesResponseType<AuctionDetails>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]

    // 409 — це «правило домену не дозволяє»: ставка нижча за мінімум, час
    // вийшов, торги закриті. Запит коректний, стан лота — ні.
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> PlaceBid(
        long listingId,
        PlaceBidRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (currentUser.Id is not { } bidderId)
        {
            return Unauthorized();
        }

        // Винятки прикладного шару перетворює на коди відповіді спільний
        // DomainExceptionHandler — ловити їх тут удруге не треба.
        var auction = await auctions.PlaceBidAsync(
            listingId,
            bidderId,
            request.MaxAmount,
            cancellationToken);

        return Ok(auction);
    }
}

/// <summary>Стеля автоставки. Окремий тип, а не голе число: сюди ще додасться підтвердження умов.</summary>
public sealed record PlaceBidRequest(decimal MaxAmount);
