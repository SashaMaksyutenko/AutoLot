namespace AutoLot.Infrastructure.Identity;

/// <summary>
/// Імена клеймів у токені. Короткі, без URI-схем ASP.NET за замовчуванням —
/// токен лишається компактним, а на боці API вимкнено мапінг вхідних клеймів.
/// </summary>
public static class AutoLotClaims
{
    public const string Subject = "sub";
    public const string Email = "email";
    public const string Name = "name";
    public const string Role = "role";
    public const string TokenId = "jti";
    public const string AccountType = "account_type";
}
