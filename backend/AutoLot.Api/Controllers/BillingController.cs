using AutoLot.Application.Billing;
using AutoLot.Application.Billing.Dtos;
using AutoLot.Application.Common.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutoLot.Api.Controllers;

/// <summary>
/// Гаманець і тарифи.
///
/// Усе закрито токеном, крім переліку планів: ціни й ліміти має бачити й
/// гість — інакше він не зрозуміє, що дає реєстрація. Але свій баланс і своє
/// оформлення бачить лише власник.
/// </summary>
[ApiController]
[Route("api/billing")]
[Authorize]
public sealed class BillingController(
    IBillingService billing,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Перелік тарифів. Для гостя — без позначки «чинний».</summary>
    [HttpGet("plans")]
    [AllowAnonymous]
    [ProducesResponseType<IReadOnlyList<PlanCard>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPlans(CancellationToken cancellationToken)
    {
        return Ok(await billing.GetPlansAsync(currentUser.Id, cancellationToken));
    }

    [HttpGet("wallet")]
    [ProducesResponseType<WalletState>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetWallet(CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
        {
            return Unauthorized();
        }

        return Ok(await billing.GetWalletAsync(userId, cancellationToken));
    }

    /// <summary>
    /// Поповнення. Справжніх платежів у проєкті немає — сума нараховується
    /// одразу; це демонстрація механіки, а не заглушка платіжної системи.
    /// </summary>
    [HttpPost("wallet/top-up")]
    [ProducesResponseType<WalletState>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> TopUp(
        TopUpRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (currentUser.Id is not { } userId)
        {
            return Unauthorized();
        }

        return Ok(await billing.TopUpAsync(userId, request.Amount, cancellationToken));
    }

    [HttpGet("subscription")]
    [ProducesResponseType<SubscriptionState>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetSubscription(CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
        {
            return Unauthorized();
        }

        return Ok(await billing.GetSubscriptionAsync(userId, cancellationToken));
    }

    /// <summary>Оформлює тариф, списавши його вартість із балансу.</summary>
    [HttpPost("subscription/{planCode}")]
    [ProducesResponseType<SubscriptionState>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Subscribe(
        string planCode,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
        {
            return Unauthorized();
        }

        return Ok(await billing.SubscribeAsync(userId, planCode, cancellationToken));
    }
}
