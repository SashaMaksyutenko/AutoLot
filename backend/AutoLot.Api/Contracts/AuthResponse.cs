using AutoLot.Application.Auth.Dtos;

namespace AutoLot.Api.Contracts;

/// <summary>
/// Те, що бачить клієнт. Refresh-токена тут навмисно немає: він живе виключно
/// в httpOnly cookie, недоступній для JavaScript.
/// </summary>
public sealed record AuthResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    UserProfile User);
