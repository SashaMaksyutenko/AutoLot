namespace AutoLot.Api.Auth;

/// <summary>
/// Refresh-токен ніколи не потрапляє в тіло відповіді — лише в httpOnly cookie
/// (SPEC §8), тож XSS на фронтенді не дає його вкрасти. Шлях звужено до
/// /api/auth, щоб токен не їздив із кожним запитом до каталогу.
/// </summary>
public static class RefreshTokenCookie
{
    public const string Name = "autolot.refresh";

    private const string Path = "/api/auth";

    public static void Write(HttpResponse response, string token, DateTimeOffset expiresAt)
    {
        ArgumentNullException.ThrowIfNull(response);

        response.Cookies.Append(Name, token, BuildOptions(response, expiresAt));
    }

    public static string? Read(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.Cookies.TryGetValue(Name, out var token) && !string.IsNullOrWhiteSpace(token)
            ? token
            : null;
    }

    public static void Clear(HttpResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        response.Cookies.Delete(Name, BuildOptions(response, DateTimeOffset.UnixEpoch));
    }

    private static CookieOptions BuildOptions(HttpResponse response, DateTimeOffset expiresAt) => new()
    {
        HttpOnly = true,
        SameSite = SameSiteMode.Strict,

        // У розробці фронт ходить по http://localhost, тому Secure не жорсткий;
        // у продакшені HTTPS обов'язковий і cookie стає Secure автоматично.
        Secure = response.HttpContext.Request.IsHttps,
        Path = Path,
        Expires = expiresAt,
        IsEssential = true,
    };
}
