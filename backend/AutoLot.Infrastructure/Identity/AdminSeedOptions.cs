namespace AutoLot.Infrastructure.Identity;

/// <summary>
/// Перший адміністратор (SPEC §3). Порожні значення означають «не створювати» —
/// пароля за замовчуванням у коді немає навмисно.
/// </summary>
public sealed class AdminSeedOptions
{
    public const string SectionName = "Seed:Admin";

    public string? Email { get; set; }

    public string? Password { get; set; }

    public string DisplayName { get; set; } = "Адміністратор";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Email) && !string.IsNullOrWhiteSpace(Password);
}
