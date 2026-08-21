using AutoLot.Application.Auth.Dtos;

namespace AutoLot.Application.Auth;

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(
        RegisterRequest request,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    Task<AuthResult> LoginAsync(
        LoginRequest request,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Обмінює refresh-токен на нову пару. Старий токен гаситься одразу:
    /// повторна спроба з ним означає крадіжку і вбиває всю сім'ю токенів.
    /// </summary>
    Task<AuthResult> RefreshAsync(
        string refreshToken,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    /// <summary>Вихід: гасить сім'ю токенів, до якої належить переданий.</summary>
    Task RevokeAsync(
        string refreshToken,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Вхід через зовнішнього провайдера. Якщо email уже зареєстровано паролем —
    /// прив'язуємо логін до наявного акаунта, а не створюємо другий.
    /// </summary>
    Task<AuthResult> SignInWithExternalAsync(
        ExternalLogin login,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    Task<UserProfile?> GetProfileAsync(long userId, CancellationToken cancellationToken = default);
}
