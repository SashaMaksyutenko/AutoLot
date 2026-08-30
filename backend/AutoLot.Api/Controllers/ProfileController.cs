using AutoLot.Application.Auth.Dtos;
using AutoLot.Application.Common.Abstractions;
using AutoLot.Application.Users;
using AutoLot.Application.Users.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutoLot.Api.Controllers;

/// <summary>
/// Профіль поточного користувача. Ідентифікатор береться з токена, а не з
/// маршруту, тож змінити чужий профіль неможливо навіть підбором номера.
/// </summary>
[ApiController]
[Route("api/profile")]
[Authorize]
public sealed class ProfileController(
    IUserProfileService profileService,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpPut]
    [ProducesResponseType<UserProfile>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Update(
        UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
        {
            return Unauthorized();
        }

        var profile = await profileService.UpdateAsync(userId, request, cancellationToken);

        return profile is null ? NotFound() : Ok(profile);
    }

    [HttpPut("location")]
    [ProducesResponseType<UserProfile>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateLocation(
        UpdateLocationRequest request,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
        {
            return Unauthorized();
        }

        // Некоректне місцезнаходження перетворює на 400 спільний
        // DomainExceptionHandler — тут ловити його вдруге не треба.
        var profile = await profileService.UpdateLocationAsync(userId, request, cancellationToken);

        return profile is null ? NotFound() : Ok(profile);
    }
}
