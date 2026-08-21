using System.Security.Claims;
using AutoLot.Api.Auth;
using AutoLot.Api.Contracts;
using AutoLot.Api.Extensions;
using AutoLot.Application.Auth;
using AutoLot.Application.Auth.Dtos;
using AutoLot.Application.Common.Abstractions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AutoLot.Api.Controllers;

[ApiController]
[Route("api/auth")]
[EnableRateLimiting(RateLimitingSetup.AuthPolicy)]
public sealed class AuthController(
    IAuthService authService,
    ICurrentUser currentUser,
    IConfiguration configuration) : ControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        return Respond(await authService.RegisterAsync(request, RemoteIp(), cancellationToken));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        return Respond(await authService.LoginAsync(request, RemoteIp(), cancellationToken));
    }

    /// <summary>Обмінює refresh-токен із cookie на нову пару токенів.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        var token = RefreshTokenCookie.Read(Request);

        if (token is null)
        {
            return ToProblem(AuthResult.Failure(AuthError.InvalidRefreshToken, "Сесія відсутня."));
        }

        var result = await authService.RefreshAsync(token, RemoteIp(), cancellationToken);

        if (!result.Succeeded)
        {
            // Недійсну cookie прибираємо, щоб клієнт не ходив із нею по колу.
            RefreshTokenCookie.Clear(Response);
        }

        return Respond(result);
    }

    /// <summary>
    /// Вихід. Доступний і без валідного access-токена: якщо той уже протух,
    /// користувач усе одно має мати змогу погасити сесію.
    /// </summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var token = RefreshTokenCookie.Read(Request);

        if (token is not null)
        {
            await authService.RevokeAsync(token, RemoteIp(), cancellationToken);
        }

        RefreshTokenCookie.Clear(Response);

        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType<UserProfile>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
        {
            return Unauthorized();
        }

        var profile = await authService.GetProfileAsync(userId, cancellationToken);

        return profile is null ? NotFound() : Ok(profile);
    }

    /// <summary>Починає вхід через Google: віддає редірект на згоду користувача.</summary>
    [HttpGet("google/start")]
    [AllowAnonymous]
    public IActionResult GoogleStart([FromQuery] string? returnUrl)
    {
        if (!AuthenticationSetup.IsGoogleConfigured(configuration))
        {
            return Problem(
                title: "Вхід через Google не налаштований",
                detail: "Задайте Authentication:Google:ClientId та ClientSecret у user-secrets.",
                statusCode: StatusCodes.Status501NotImplemented);
        }

        var properties = new AuthenticationProperties
        {
            RedirectUri = Url.Action(nameof(GoogleCallback), values: new { returnUrl }),
        };

        return Challenge(properties, AuthenticationSetup.GoogleProvider);
    }

    /// <summary>
    /// Приймає користувача назад від Google. Access-токен у URL не кладемо —
    /// ставимо лише refresh-cookie, а фронтенд одразу викликає /refresh.
    /// </summary>
    [HttpGet("google/callback")]
    [AllowAnonymous]
    public async Task<IActionResult> GoogleCallback(
        [FromQuery] string? returnUrl,
        CancellationToken cancellationToken)
    {
        var target = ResolveReturnUrl(returnUrl);

        var authentication = await HttpContext.AuthenticateAsync(AuthenticationSetup.ExternalScheme);

        // Проміжна cookie більше не потрібна незалежно від результату.
        await HttpContext.SignOutAsync(AuthenticationSetup.ExternalScheme);

        if (!authentication.Succeeded || authentication.Principal is null)
        {
            return Redirect(WithError(target, "external_failed"));
        }

        var principal = authentication.Principal;
        var providerKey = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = principal.FindFirstValue(ClaimTypes.Email);

        if (string.IsNullOrWhiteSpace(providerKey) || string.IsNullOrWhiteSpace(email))
        {
            return Redirect(WithError(target, "external_failed"));
        }

        var login = new ExternalLogin(
            AuthenticationSetup.GoogleProvider,
            providerKey,
            email,
            principal.FindFirstValue(ClaimTypes.Name),
            string.Equals(principal.FindFirstValue("email_verified"), "true", StringComparison.OrdinalIgnoreCase));

        var result = await authService.SignInWithExternalAsync(login, RemoteIp(), cancellationToken);

        if (!result.Succeeded || result.Tokens is null)
        {
            return Redirect(WithError(target, result.Error.ToString()));
        }

        RefreshTokenCookie.Write(Response, result.Tokens.RefreshToken, result.Tokens.RefreshTokenExpiresAt);

        return Redirect(target);
    }

    private IActionResult Respond(AuthResult result)
    {
        if (!result.Succeeded || result.Tokens is null)
        {
            return ToProblem(result);
        }

        RefreshTokenCookie.Write(Response, result.Tokens.RefreshToken, result.Tokens.RefreshTokenExpiresAt);

        return Ok(new AuthResponse(
            result.Tokens.AccessToken,
            result.Tokens.AccessTokenExpiresAt,
            result.Tokens.Profile));
    }

    private ObjectResult ToProblem(AuthResult result)
    {
        var (statusCode, title) = result.Error switch
        {
            AuthError.EmailAlreadyUsed => (StatusCodes.Status409Conflict, "Email уже зайнято"),
            AuthError.InvalidCredentials => (StatusCodes.Status401Unauthorized, "Не вдалося увійти"),
            AuthError.AccountLockedOut => (StatusCodes.Status423Locked, "Акаунт тимчасово заблоковано"),
            AuthError.AccountBanned => (StatusCodes.Status403Forbidden, "Акаунт заблоковано"),
            AuthError.InvalidRefreshToken => (StatusCodes.Status401Unauthorized, "Сесія недійсна"),
            AuthError.PasswordRejected => (StatusCodes.Status400BadRequest, "Пароль не відповідає вимогам"),
            AuthError.ExternalLoginFailed => (StatusCodes.Status400BadRequest, "Зовнішній вхід не вдався"),
            _ => (StatusCodes.Status400BadRequest, "Помилка автентифікації"),
        };

        return Problem(
            title: title,
            detail: string.Join(" ", result.Messages),
            statusCode: statusCode);
    }

    private string? RemoteIp() => HttpContext.Connection.RemoteIpAddress?.ToString();

    /// <summary>
    /// Відкритий редірект — класична дірка, тож повертаємо користувача лише
    /// на походження зі списку дозволених.
    /// </summary>
    private string ResolveReturnUrl(string? returnUrl)
    {
        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        var fallback = configuration["Frontend:BaseUrl"] ?? allowedOrigins.FirstOrDefault() ?? "/";

        if (string.IsNullOrWhiteSpace(returnUrl)
            || !Uri.TryCreate(returnUrl, UriKind.Absolute, out var uri))
        {
            return fallback;
        }

        var origin = uri.GetLeftPart(UriPartial.Authority);

        var allowed = allowedOrigins.Any(candidate =>
            string.Equals(candidate.TrimEnd('/'), origin, StringComparison.OrdinalIgnoreCase));

        return allowed ? returnUrl : fallback;
    }

    private static string WithError(string target, string code)
    {
        var separator = target.Contains('?', StringComparison.Ordinal) ? '&' : '?';

        return $"{target}{separator}authError={Uri.EscapeDataString(code)}";
    }
}
