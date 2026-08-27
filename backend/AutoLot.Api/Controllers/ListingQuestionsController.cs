using AutoLot.Application.Common.Abstractions;
using AutoLot.Application.Listings;
using AutoLot.Application.Listings.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutoLot.Api.Controllers;

/// <summary>
/// Публічні питання під оголошенням (SPEC §4). Читати може будь-хто, зокрема
/// гість: у цьому й сенс — відповідь одному цікавить усіх.
/// </summary>
[ApiController]
[Route("api/listings/{listingId:long}/questions")]
public sealed class ListingQuestionsController(
    IListingQuestionService questions,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType<IReadOnlyList<QuestionRecord>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(long listingId, CancellationToken cancellationToken)
    {
        return Ok(await questions.GetAsync(listingId, cancellationToken));
    }

    [HttpPost]
    [Authorize]
    [ProducesResponseType<QuestionRecord>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Ask(
        long listingId,
        AskQuestionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (currentUser.Id is not { } askerId)
        {
            return Unauthorized();
        }

        var question = await questions.AskAsync(listingId, askerId, request.Text, cancellationToken);

        // 201 із посиланням на список: окремої адреси в питання немає, та й
        // дивитися його поодинці немає потреби.
        return Created($"/api/listings/{listingId}/questions", question);
    }

    /// <summary>
    /// Відповідь продавця. Номер продавця беремо з токена — надіслати чужий
    /// у тілі запиту неможливо, бо там його просто немає.
    /// </summary>
    [HttpPut("{questionId:long}/answer")]
    [Authorize]
    [ProducesResponseType<QuestionRecord>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Answer(
        long listingId,
        long questionId,
        AnswerQuestionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (currentUser.Id is not { } sellerId)
        {
            return Unauthorized();
        }

        return Ok(await questions.AnswerAsync(questionId, sellerId, request.Text, cancellationToken));
    }
}
