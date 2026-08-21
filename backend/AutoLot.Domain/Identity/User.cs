using AutoLot.Domain.Common;
using AutoLot.Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace AutoLot.Domain.Identity;

/// <summary>
/// Користувач майданчика. Успадковує IdentityUser заради готової та перевіреної
/// механіки паролів, блокувань і зовнішніх логінів; email, телефон і їхні
/// підтвердження вже є в базовому типі.
/// </summary>
public class User : IdentityUser<long>, IAuditable
{
    /// <summary>Ім'я, яке бачать інші користувачі. Для дилера — назва салону.</summary>
    public string DisplayName { get; set; } = string.Empty;

    public AccountType AccountType { get; set; } = AccountType.Private;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public DateTimeOffset? LastLoginAt { get; set; }

    /// <summary>
    /// Заблокований модератором акаунт. Це не Identity-lockout: той тимчасовий
    /// і спрацьовує від невдалих спроб входу.
    /// </summary>
    public bool IsBanned { get; set; }

    public ICollection<RefreshToken> RefreshTokens { get; } = [];
}
