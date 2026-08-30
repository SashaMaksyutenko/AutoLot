using AutoLot.Application.Chat;
using AutoLot.Application.Chat.Dtos;
using AutoLot.Application.Common.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutoLot.Api.Controllers;

/// <summary>
/// Приватне листування. Усе закрито токеном: чужа переписка не має бути
/// доступна навіть на читання, тож роль перевіряється на рівні контролера.
/// </summary>
[ApiController]
[Route("api/chat")]
[Authorize]
public sealed class ChatController(
    IChatService chat,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("conversations")]
    [ProducesResponseType<IReadOnlyList<ConversationSummary>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
        {
            return Unauthorized();
        }

        return Ok(await chat.GetMineAsync(userId, cancellationToken));
    }

    [HttpGet("unread")]
    [ProducesResponseType<UnreadCount>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetUnread(CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
        {
            return Unauthorized();
        }

        return Ok(new UnreadCount(await chat.GetUnreadCountAsync(userId, cancellationToken)));
    }

    /// <summary>
    /// Починає розмову про оголошення або відкриває вже наявну. Повторний
    /// виклик безпечний — одна гілка на пару «оголошення + покупець».
    /// </summary>
    [HttpPost("conversations/{listingId:long}")]
    [ProducesResponseType<ConversationDetails>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Start(long listingId, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } buyerId)
        {
            return Unauthorized();
        }

        return Ok(await chat.StartAsync(listingId, buyerId, cancellationToken));
    }

    /// <summary>Стрічка розмови. Відкриття позначає чужі повідомлення прочитаними.</summary>
    [HttpGet("conversations/{conversationId:long}/messages")]
    [ProducesResponseType<ConversationDetails>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMessages(
        long conversationId,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
        {
            return Unauthorized();
        }

        return Ok(await chat.GetAsync(conversationId, userId, cancellationToken));
    }

    [HttpPost("conversations/{conversationId:long}/messages")]
    [ProducesResponseType<MessageRecord>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Send(
        long conversationId,
        SendMessageRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (currentUser.Id is not { } senderId)
        {
            return Unauthorized();
        }

        var message = await chat.SendAsync(conversationId, senderId, request.Text, cancellationToken);

        return Created($"/api/chat/conversations/{conversationId}/messages", message);
    }
}

/// <summary>Обгортка навколо числа: голе число в тілі відповіді розширювати нікуди.</summary>
public sealed record UnreadCount(int Count);
