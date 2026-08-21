using System.Security.Cryptography;
using System.Text;

namespace AutoLot.Infrastructure.Identity;

/// <summary>
/// Refresh-токен — це просто випадкові байти. Сенсу в них немає, тому підпис
/// не потрібен; у базі лежить лише хеш, щоб її витік не давав входу.
/// </summary>
public static class RefreshTokenFactory
{
    private const int TokenSizeInBytes = 32;

    public static string Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(TokenSizeInBytes);

        // base64url: токен їде в cookie, а '+' та '/' там зайві.
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    /// <summary>
    /// SHA-256 без солі свідомо: на вході 256 біт ентропії, тож перебір і
    /// райдужні таблиці безсилі, а пошук за хешем лишається одним індексом.
    /// </summary>
    public static string Hash(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }
}
