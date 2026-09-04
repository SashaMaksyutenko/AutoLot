using AutoLot.Application.Common.Abstractions;
using AutoLot.Application.Search;
using AutoLot.Application.Search.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutoLot.Api.Controllers;

/// <summary>
/// Збережені пошуки. Усе закрито токеном: чужі фільтри — це чужий намір
/// щось купити, і стороннім він не показується навіть на читання.
/// </summary>
[ApiController]
[Route("api/saved-searches")]
[Authorize]
public sealed class SavedSearchesController(
    ISavedSearchService searches,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<SavedSearchCard>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
        {
            return Unauthorized();
        }

        return Ok(await searches.GetMineAsync(userId, cancellationToken));
    }

    /// <summary>Зберігає поточні фільтри каталогу під назвою.</summary>
    [HttpPost]
    [ProducesResponseType<SavedSearchCard>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Save(
        SaveSearchRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (currentUser.Id is not { } userId)
        {
            return Unauthorized();
        }

        var saved = await searches.SaveAsync(userId, request.Name, request.Query, cancellationToken);

        return Created($"/api/saved-searches/{saved.Id}", saved);
    }

    [HttpPut("{searchId:long}")]
    [ProducesResponseType<SavedSearchCard>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Rename(
        long searchId,
        RenameSearchRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (currentUser.Id is not { } userId)
        {
            return Unauthorized();
        }

        return Ok(await searches.RenameAsync(searchId, userId, request.Name, cancellationToken));
    }

    [HttpDelete("{searchId:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(long searchId, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
        {
            return Unauthorized();
        }

        await searches.DeleteAsync(searchId, userId, cancellationToken);

        return NoContent();
    }
}
