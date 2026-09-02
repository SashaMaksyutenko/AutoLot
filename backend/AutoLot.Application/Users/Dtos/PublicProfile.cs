using AutoLot.Application.Listings.Dtos;
using AutoLot.Domain.Enums;

namespace AutoLot.Application.Users.Dtos;

/// <summary>
/// Профіль продавця очима стороннього.
///
/// Окремий тип від <c>UserProfile</c>, а не той самий із порожніми полями.
/// Різниця не косметична: там пошта, телефон, ролі й підтвердження — усе,
/// чого стороннім бачити не можна. Спільний тип із «не заповнюй ці поля»
/// рано чи пізно віддав би їх назовні, бо забути одне поле легше, ніж
/// створити цілий зайвий тип.
/// </summary>
public sealed record PublicProfile(
    long Id,
    string DisplayName,
    AccountType AccountType,

    /// <summary>
    /// Відколи на майданчику. Найдешевша ознака довіри, яка тут є: акаунт,
    /// створений три роки тому, поводиться інакше, ніж вчорашній.
    /// </summary>
    DateTimeOffset JoinedAt,

    /// <summary>Місто, якщо вказане. Адреси не показуємо — лише місто.</summary>
    string? CityName,

    RatingSummary Rating,

    /// <summary>Скільки оголошень зараз опубліковано.</summary>
    int ActiveListingCount,

    /// <summary>
    /// Салон, у якому людина працює, якщо працює. Покупцеві важливо бачити,
    /// що перед ним не приватна особа.
    /// </summary>
    DealerBadge? Dealer);
