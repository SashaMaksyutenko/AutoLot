using AutoLot.Domain.Dealers;

namespace AutoLot.Application.Dealers.Dtos;

/// <summary>Публічна картка салону.</summary>
public sealed record DealershipDetails(
    long Id,
    string Name,
    string Slug,
    string? Description,
    string? LogoPath,
    string CityName,
    bool IsVerified,
    DateTimeOffset? VerifiedAt,

    /// <summary>Скільки в салоні активних оголошень — перше, що цікавить покупця.</summary>
    int ActiveListingCount);

/// <summary>Салон, у якому працює користувач, і його роль там.</summary>
public sealed record DealershipMembership(
    long DealershipId,
    string Name,
    string Slug,
    DealershipRole Role,
    bool IsVerified);

/// <summary>Співробітник у списку персоналу. Email видно лише своїм.</summary>
public sealed record StaffMember(
    long UserId,
    string DisplayName,
    string Email,
    DealershipRole Role,
    DateTimeOffset JoinedAt);

public sealed record CreateDealershipRequest
{
    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public long CityId { get; init; }
}

/// <summary>Кого додаємо в салон і ким.</summary>
public sealed record AddStaffRequest
{
    /// <summary>
    /// Пошта, а не номер користувача: власник салону знає пошту колеги,
    /// а внутрішній номер — ні.
    /// </summary>
    public string Email { get; init; } = string.Empty;

    public DealershipRole Role { get; init; } = DealershipRole.Manager;
}
