using AutoLot.Application.Geo.Dtos;
using AutoLot.Domain.Enums;

namespace AutoLot.Application.Auth.Dtos;

public sealed record UserProfile(
    long Id,
    string Email,
    string DisplayName,
    AccountType AccountType,
    bool EmailConfirmed,

    /// <summary>Порожній, поки не вказали. Показується лише автентифікованим.</summary>
    string? PhoneNumber,
    bool PhoneNumberConfirmed,
    IReadOnlyList<string> Roles,
    /// <summary>Порожнє, поки користувач не вказав, звідки він.</summary>
    UserLocation? Location);
