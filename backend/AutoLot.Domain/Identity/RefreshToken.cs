using AutoLot.Domain.Common;

namespace AutoLot.Domain.Identity;

/// <summary>
/// Refresh-токен зберігається лише хешем: витік бази не має давати можливості
/// увійти. Токени однієї сесії пов'язані через <see cref="FamilyId"/> — якщо
/// вкрадений токен спробують використати вдруге, гасимо всю сім'ю разом.
/// </summary>
public class RefreshToken : Entity
{
    public long UserId { get; set; }

    public User User { get; set; } = null!;

    /// <summary>SHA-256 від значення токена, у base64.</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>Спільний ідентифікатор ланцюжка ротацій одного входу.</summary>
    public Guid FamilyId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public string? RevokedReason { get; set; }

    /// <summary>IP, з якої токен видано. Потрібна для аудиту підозрілих сесій.</summary>
    public string? CreatedByIp { get; set; }

    public bool IsRevoked => RevokedAt is not null;

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;

    public bool IsActive(DateTimeOffset now) => !IsRevoked && !IsExpired(now);
}
