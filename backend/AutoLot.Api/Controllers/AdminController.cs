using AutoLot.Application.Admin;
using AutoLot.Application.Admin.Dtos;
using AutoLot.Application.Common;
using AutoLot.Application.Common.Abstractions;
using AutoLot.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutoLot.Api.Controllers;

/// <summary>
/// Адмінка: люди, ролі, показники.
///
/// Роль перевіряється на рівні контролера, і **лише адміністратор**:
/// модератор працює з оголошеннями, а не з людьми. Так жоден новий метод
/// не може випадково лишитися відкритим ширше, ніж треба.
/// </summary>
[ApiController]
[Route("api/admin")]
[Authorize(Roles = RoleNames.Admin)]
public sealed class AdminController(
    IAdminService admin,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("stats")]
    [ProducesResponseType<PlatformStats>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStats(CancellationToken cancellationToken)
    {
        return Ok(await admin.GetStatsAsync(cancellationToken));
    }

    [HttpGet("users")]
    [ProducesResponseType<PagedResult<UserSummary>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchUsers(
        [FromQuery] UserSearchQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await admin.SearchUsersAsync(query, cancellationToken));
    }

    [HttpPut("users/{userId:long}/ban")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetBanned(
        long userId,
        SetBannedRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (currentUser.Id is not { } adminId)
        {
            return Unauthorized();
        }

        await admin.SetBannedAsync(userId, adminId, request.IsBanned, cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Призначає або знімає роль. Саме тут з'являються модератори — інакше
    /// кожен новий вимагав би правки конфігурації й перезапуску сервера.
    /// </summary>
    [HttpPut("users/{userId:long}/roles")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetRole(
        long userId,
        SetRoleRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (currentUser.Id is not { } adminId)
        {
            return Unauthorized();
        }

        await admin.SetRoleAsync(userId, adminId, request.Role, request.Granted, cancellationToken);

        return NoContent();
    }
}
