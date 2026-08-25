using AutoLot.Application.Common;
using AutoLot.Application.Common.Abstractions;
using AutoLot.Application.Favorites;
using AutoLot.Application.Listings.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutoLot.Api.Controllers;

/// <summary>
/// Обране поточного користувача. Чиє саме обране — визначає токен, а не
/// маршрут: номера користувача в адресі немає взагалі, тож зазирнути в чужий
/// список неможливо навіть підбором.
/// </summary>
[ApiController]
[Route("api/favorites")]
[Authorize]
public sealed class FavoritesController(
    IFavoriteService favorites,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<ListingSummary>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetPage(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 24,
        CancellationToken cancellationToken = default)
    {
        if (currentUser.Id is not { } userId)
        {
            return Unauthorized();
        }

        return Ok(await favorites.GetPageAsync(userId, page, pageSize, cancellationToken));
    }

    [HttpGet("count")]
    [ProducesResponseType<FavoriteCount>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetCount(CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
        {
            return Unauthorized();
        }

        return Ok(new FavoriteCount(await favorites.CountAsync(userId, cancellationToken)));
    }

    /// <summary>
    /// Ставить позначку. Повторний виклик нічого не змінює й теж вважається
    /// успіхом: натиснути «в обране» двічі — не помилка користувача.
    /// </summary>
    [HttpPut("{listingId:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Add(long listingId, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
        {
            return Unauthorized();
        }

        // Неіснуюче або неопубліковане оголошення перетворює на 404 спільний
        // DomainExceptionHandler — ловити виняток тут удруге не треба.
        await favorites.AddAsync(userId, listingId, cancellationToken);

        return NoContent();
    }

    [HttpDelete("{listingId:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Remove(long listingId, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
        {
            return Unauthorized();
        }

        // Зняти те, чого не було, — теж 204: результат саме той, якого хотіли.
        await favorites.RemoveAsync(userId, listingId, cancellationToken);

        return NoContent();
    }
}

/// <summary>Обгортка навколо числа: голе число в тілі відповіді розширювати нікуди.</summary>
public sealed record FavoriteCount(int Count);
