using AutoLot.Application.Bots;
using AutoLot.Application.Common.Abstractions;
using AutoLot.Domain.Bots;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutoLot.Api.Controllers;

/// <summary>
/// Прив'язка месенджерів до акаунта.
///
/// Код видається саме тут, у кабінеті, куди людина вже увійшла. Бот не може
/// впізнати її сам: у чаті він бачить лише розмову, а питати пароль у
/// месенджері не можна — саме так і виглядає крадіжка облікових даних.
/// </summary>
[ApiController]
[Route("api/bots")]
[Authorize]
public sealed class BotsController(
    IBotLinkService links,
    IBotDirectory directory,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>
    /// Чи піднятий телеграм-бот і як він називається.
    ///
    /// Відкрито без входу: кабінет питає це, щоб вирішити, чи показувати
    /// картку взагалі, а таємниці тут немає — ім'я бота й так публічне.
    /// </summary>
    [HttpGet("telegram")]
    [AllowAnonymous]
    [ProducesResponseType<TelegramBotInfo>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTelegram(CancellationToken cancellationToken)
    {
        return Ok(await directory.GetTelegramAsync(cancellationToken));
    }

    /// <summary>Видає одноразовий код для команди /link у боті.</summary>
    [HttpPost("link-code")]
    [ProducesResponseType<BotLinkCodeIssued>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> IssueCode(CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
        {
            return Unauthorized();
        }

        return Ok(await links.IssueCodeAsync(userId, cancellationToken));
    }

    /// <summary>Які месенджери вже прив'язані — щоб кабінет це показав.</summary>
    [HttpGet("links")]
    [ProducesResponseType<IReadOnlyList<BotProvider>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetLinks(CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
        {
            return Unauthorized();
        }

        var recipients = await links.GetRecipientsAsync(userId, cancellationToken);

        // Назовні віддаємо лише перелік месенджерів. Ідентифікатор чату —
        // це адреса, за якою людині можна писати, і сторінці вона не потрібна.
        return Ok(recipients.Select(recipient => recipient.Provider).Distinct());
    }
}
