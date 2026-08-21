namespace AutoLot.Application.Auth.Dtos;

/// <summary>
/// Пара токенів. Refresh назовні віддається лише в httpOnly cookie — у тілі
/// відповіді його немає, тому контролер бере його звідси й не серіалізує.
/// </summary>
public sealed record AuthTokens(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    UserProfile Profile);
