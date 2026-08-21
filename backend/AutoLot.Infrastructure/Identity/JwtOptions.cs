using System.ComponentModel.DataAnnotations;

namespace AutoLot.Infrastructure.Identity;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>
    /// Ключ підпису HS256. Живе в user-secrets; 32 байти — мінімум, менший
    /// ключ алгоритм просто не прийме.
    /// </summary>
    [Required(ErrorMessage = "Jwt:Key не налаштований — задайте його в user-secrets.")]
    [MinLength(32, ErrorMessage = "Jwt:Key має бути щонайменше 32 символи.")]
    public string Key { get; set; } = string.Empty;

    [Required]
    public string Issuer { get; set; } = "AutoLot";

    [Required]
    public string Audience { get; set; } = "AutoLot";

    /// <summary>Access-токен живе коротко — компрометація дорого не коштує (SPEC §8).</summary>
    [Range(1, 120)]
    public int AccessTokenMinutes { get; set; } = 15;

    [Range(1, 365)]
    public int RefreshTokenDays { get; set; } = 30;
}
