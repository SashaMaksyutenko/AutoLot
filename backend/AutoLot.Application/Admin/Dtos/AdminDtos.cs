using AutoLot.Domain.Enums;

namespace AutoLot.Application.Admin.Dtos;

/// <summary>
/// Людина в списку адмінки. Пошта тут є навмисно: без неї адміністратор не
/// зможе знайти потрібного серед тезок.
/// </summary>
public sealed record UserSummary(
    long Id,
    string DisplayName,
    string Email,
    AccountType AccountType,
    bool IsBanned,
    bool EmailConfirmed,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt,
    IReadOnlyList<string> Roles,

    /// <summary>Скільки активних оголошень — швидка ознака, чи людина працює.</summary>
    int ActiveListingCount);

public sealed record UserSearchQuery
{
    /// <summary>Пошук за іменем або поштою одночасно — адміністратор може знати будь-що.</summary>
    public string? Text { get; init; }

    public bool? IsBanned { get; init; }

    public string? Role { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 25;
}

/// <summary>
/// Показники майданчика. Свідомо кілька чисел, а не звіт: головна адмінки
/// має відповідати на питання «чи все гаразд», а не заміняти аналітику.
/// </summary>
public sealed record PlatformStats(
    int TotalUsers,
    int BannedUsers,
    int ActiveListings,
    int PendingModeration,

    /// <summary>Скарги, що чекають розгляду. Черга окрема від модерації.</summary>
    int PendingReports,
    int ActiveAuctions,
    int Dealerships,
    int UnverifiedDealerships);

/// <summary>Тіло запиту на блокування.</summary>
public sealed record SetBannedRequest
{
    public bool IsBanned { get; init; }
}

/// <summary>Тіло запиту на зміну ролі.</summary>
public sealed record SetRoleRequest
{
    public string Role { get; init; } = string.Empty;

    public bool Granted { get; init; }
}
