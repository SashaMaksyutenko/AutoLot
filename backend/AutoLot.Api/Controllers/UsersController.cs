using AutoLot.Application.Users;
using AutoLot.Application.Users.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutoLot.Api.Controllers;

/// <summary>
/// Публічний профіль продавця.
///
/// Окремий контролер від <c>ProfileController</c>, і не заради краси: той
/// закритий токеном і працює з ВЛАСНИМ профілем, де є пошта, телефон і ролі.
/// Тут усе відкрито, тож змішувати їх в одному класі означало б покладатися
/// на те, що ніхто не забуде атрибут над новим методом.
/// </summary>
[ApiController]
[Route("api/users")]
[AllowAnonymous]
public sealed class UsersController(IPublicProfileService profiles) : ControllerBase
{
    /// <summary>Профіль продавця: відколи на майданчику, рейтинг, скільки авто продає.</summary>
    [HttpGet("{userId:long}")]
    [ProducesResponseType<PublicProfile>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPublic(long userId, CancellationToken cancellationToken)
    {
        var profile = await profiles.GetAsync(userId, cancellationToken);

        return profile is null ? NotFound() : Ok(profile);
    }
}
