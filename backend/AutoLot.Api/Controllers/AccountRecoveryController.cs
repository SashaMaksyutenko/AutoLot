using AutoLot.Api.Extensions;
using AutoLot.Application.Auth;
using AutoLot.Application.Auth.Dtos;
using AutoLot.Application.Common.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AutoLot.Api.Controllers;

/// <summary>
/// Відновлення пароля й підтвердження пошти.
///
/// Усі дії тут відкриті без токена — інакше людина, яка забула пароль, не
/// змогла б ним скористатися. Захист інший: обмежувач частоти запитів і
/// відповіді, з яких не видно, чи існує акаунт (SPEC §8).
/// </summary>
[ApiController]
[Route("api/auth")]
public sealed class AccountRecoveryController(
    IAccountRecoveryService recovery,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>
    /// Просить лист із посиланням на зміну пароля.
    ///
    /// Завжди 202, навіть якщо такої скриньки немає. Це не недбалість:
    /// відповідь «такого користувача немає» перетворила б форму на спосіб
    /// перевіряти, хто зареєстрований на майданчику.
    /// </summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingSetup.AuthPolicy)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ForgotPassword(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        await recovery.RequestPasswordResetAsync(request.Email, cancellationToken);

        return Accepted();
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingSetup.AuthPolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword(
        ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var done = await recovery.ResetPasswordAsync(
            request.Email,
            request.Token,
            request.NewPassword,
            cancellationToken);

        // Тут причину вже можна назвати: посилання або протухло, або
        // зіпсоване. Це не розкриває, чи є такий акаунт.
        return done
            ? NoContent()
            : Problem(
                title: "Посилання недійсне",
                detail: "Термін дії посилання минув або воно пошкоджене. Попросіть новий лист.",
                statusCode: StatusCodes.Status400BadRequest);
    }

    [HttpPost("confirm-email")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingSetup.AuthPolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConfirmEmail(
        ConfirmEmailRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var done = await recovery.ConfirmEmailAsync(request.Email, request.Token, cancellationToken);

        return done
            ? NoContent()
            : Problem(
                title: "Посилання недійсне",
                detail: "Термін дії посилання минув або воно пошкоджене.",
                statusCode: StatusCodes.Status400BadRequest);
    }

    /// <summary>Надіслати лист із підтвердженням ще раз — для того, хто вже увійшов.</summary>
    [HttpPost("resend-confirmation")]
    [Authorize]
    [EnableRateLimiting(RateLimitingSetup.AuthPolicy)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ResendConfirmation(CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
        {
            return Unauthorized();
        }

        await recovery.SendEmailConfirmationAsync(userId, cancellationToken);

        return Accepted();
    }
}
