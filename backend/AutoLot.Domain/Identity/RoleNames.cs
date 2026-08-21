namespace AutoLot.Domain.Identity;

/// <summary>Ролі з SPEC §3. Рядки в одному місці, щоб не розповзалися по атрибутах.</summary>
public static class RoleNames
{
    public const string User = "User";
    public const string Moderator = "Moderator";
    public const string Admin = "Admin";

    /// <summary>Ролі, які створює сід під час старту застосунку.</summary>
    public static IReadOnlyList<string> All { get; } = [User, Moderator, Admin];
}
