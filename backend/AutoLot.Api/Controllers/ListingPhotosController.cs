using AutoLot.Application.Common.Abstractions;
using AutoLot.Application.Listings;
using AutoLot.Application.Listings.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutoLot.Api.Controllers;

/// <summary>
/// Фото оголошення. Завантажене зображення ніколи не зберігається таким, як
/// прийшло: воно перекодовується, що зрізає EXIF і будь-який вкладений вміст
/// (SPEC §8).
/// </summary>
[ApiController]
[Route("api/listings/{listingId:long}/photos")]
[Authorize]
public sealed class ListingPhotosController(
    IListingPhotoService photoService,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ListingPhoto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(long listingId, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } actorId)
        {
            return Unauthorized();
        }

        return Ok(await photoService.GetAsync(listingId, actorId, cancellationToken));
    }

    /// <summary>Додає одне фото. Перше стає головним автоматично.</summary>
    [HttpPost]
    [ProducesResponseType<ListingPhoto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [RequestSizeLimit(12 * 1024 * 1024)]
    public async Task<IActionResult> Upload(
        long listingId,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } actorId)
        {
            return Unauthorized();
        }

        if (file is null || file.Length == 0)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Файл не надіслано",
            });
        }

        await using var content = file.OpenReadStream();

        var photo = await photoService.AddAsync(
            listingId,
            actorId,
            new PhotoUpload(file.FileName, file.Length, content),
            cancellationToken);

        return CreatedAtAction(nameof(Get), new { listingId }, photo);
    }

    [HttpDelete("{photoId:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        long listingId,
        long photoId,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } actorId)
        {
            return Unauthorized();
        }

        await photoService.DeleteAsync(listingId, actorId, photoId, cancellationToken);

        return NoContent();
    }

    /// <summary>Порядок задається повним переліком фото в потрібній послідовності.</summary>
    [HttpPut("order")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Reorder(
        long listingId,
        IReadOnlyList<long> photoIds,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } actorId)
        {
            return Unauthorized();
        }

        await photoService.ReorderAsync(listingId, actorId, photoIds, cancellationToken);

        return NoContent();
    }

    [HttpPost("{photoId:long}/primary")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetPrimary(
        long listingId,
        long photoId,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } actorId)
        {
            return Unauthorized();
        }

        await photoService.SetPrimaryAsync(listingId, actorId, photoId, cancellationToken);

        return NoContent();
    }
}
