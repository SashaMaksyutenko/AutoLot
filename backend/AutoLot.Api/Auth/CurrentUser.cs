using System.Globalization;
using AutoLot.Application.Common.Abstractions;
using AutoLot.Infrastructure.Identity;

namespace AutoLot.Api.Auth;

/// <summary>
/// Читає особу виконавця запиту з перевіреного токена. Жодне поле сюди не
/// потрапляє з тіла запиту — права клієнту не належать (SPEC §8).
/// </summary>
internal sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public long? Id
    {
        get
        {
            var value = accessor.HttpContext?.User.FindFirst(AutoLotClaims.Subject)?.Value;

            return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
                ? id
                : null;
        }
    }

    public string? Email => accessor.HttpContext?.User.FindFirst(AutoLotClaims.Email)?.Value;

    public bool IsAuthenticated => accessor.HttpContext?.User.Identity?.IsAuthenticated ?? false;

    public bool IsInRole(string role) => accessor.HttpContext?.User.IsInRole(role) ?? false;
}
