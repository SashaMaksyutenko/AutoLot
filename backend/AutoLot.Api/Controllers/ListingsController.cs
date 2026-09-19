using AutoLot.Application.Common.Abstractions;
using AutoLot.Application.Listings;
using AutoLot.Application.Listings.Dtos;
using AutoLot.Domain.Enums;
using AutoLot.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AutoLot.Domain.Listings;

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
    /// <summary>Скільки авто можна порівнювати за раз. Більше не влазить на екран.</summary>
    private const int CompareLimit = 4;

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

    /// <summary>
    /// Кілька оголошень для порівняння. Одним запитом, а не чотирма: таблиця
    /// порівняння малюється цілком або не малюється зовсім, і чотири окремі
    /// відповіді лише дали б їй мигати по колонці.
    /// </summary>
    /// <summary>Оголошення для форми редагування — лише власникові.</summary>
    [HttpGet("{listingId:long}/edit")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetForEdit(long listingId, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } actorId)
        {
            return Unauthorized();
        }

        var draft = await listingService.GetForEditAsync(listingId, actorId, cancellationToken);

        return draft is null ? NotFound() : Ok(draft);
    }

    [HttpGet("compare")]
    [AllowAnonymous]
    [ProducesResponseType<IReadOnlyList<ListingDetails>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Compare(
        [FromQuery] long[] ids,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);

        // Стеля на кількість — не примха: більше чотирьох колонок не влазить
        // на екран, а без межі адресою можна було б попросити всю базу.
        var wanted = ids.Distinct().Take(CompareLimit).ToArray();

        return Ok(await listingService.GetManyAsync(wanted, cancellationToken));
    }

    /// <summary>Власні оголошення, за потреби відфільтровані за статусом.</summary>
    /// <summary>
    /// Схожі авто — для блоку під карткою. Без входу: це така сама частина
    /// сторінки, як фото чи опис.
    /// </summary>
    [HttpGet("{listingId:long}/similar")]
    [AllowAnonymous]
    [ProducesResponseType<IReadOnlyList<ListingSummary>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Similar(
        long listingId,
        [FromQuery] int take = 4,
        CancellationToken cancellationToken = default)
    {
        // Межа зверху — щоб запит не перетворився на другий каталог.
        var wanted = Math.Clamp(take, 1, 12);

        return Ok(await listingService.GetSimilarAsync(listingId, wanted, cancellationToken));
    }

    /// <summary>
    /// Змінює ціну опублікованого оголошення.
    ///
    /// Окремо від PUT /api/listings/{id}: те редагує все й лише чернетку, а
    /// це — саму ціну й лише в опублікованому. Модерацію зміна ціни не
    /// проходить, бо ціна й є тим, що продавець рухає щодня.
    /// </summary>
    [HttpPut("{listingId:long}/price")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ChangePrice(
        long listingId,
        ChangePriceRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (currentUser.Id is not { } actorId)
        {
            return Unauthorized();
        }

        await listingService.ChangePriceAsync(
            listingId,
            actorId,
            request.Price,
            request.Currency,
            cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Як мінялася ціна. Доступно без входу — для опублікованого оголошення
    /// це така сама частина картки, як пробіг чи рік.
    /// </summary>
    [HttpGet("{listingId:long}/price-history")]
    [AllowAnonymous]
    [ProducesResponseType<IReadOnlyList<PriceHistoryPoint>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> PriceHistory(long listingId, CancellationToken cancellationToken)
    {
        return Ok(await listingService.GetPriceHistoryAsync(listingId, currentUser.Id, cancellationToken));
    }

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

    /// <summary>Що я купив на майданчику.</summary>
    /// <summary>Історія переглядів: що людина відкривала останнім.</summary>
    [HttpGet("recently-viewed")]
    [Authorize]
    [ProducesResponseType<IReadOnlyList<ListingSummary>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RecentlyViewed(
        [FromQuery] int take = 4,
        CancellationToken cancellationToken = default)
    {
        if (currentUser.Id is not { } userId)
        {
            return Unauthorized();
        }

        /*
          Скільки показати, вирішує сторінка: під карткою авто доречні
          чотири, на окремій сторінці історії — усе, що ми взагалі
          зберігаємо.

          Але межу ставимо ми, а не той, хто питає: число приходить із
          адресного рядка, і без обмеження будь-хто попросив би мільйон
          рядків одним запитом.
        */
        var wanted = Math.Clamp(take, 1, ListingView.PerUserLimit);

        return Ok(await listingService.GetRecentlyViewedAsync(userId, wanted, cancellationToken));
    }

    [HttpGet("purchased")]
    [ProducesResponseType<IReadOnlyList<ListingSummary>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetPurchased(CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } buyerId)
        {
            return Unauthorized();
        }

        return Ok(await listingService.GetPurchasedAsync(buyerId, cancellationToken));
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
