using AutoLot.Domain.Enums;

namespace AutoLot.Application.Auth.Dtos;

public sealed record UserProfile(
    long Id,
    string Email,
    string DisplayName,
    AccountType AccountType,
    bool EmailConfirmed,
    bool PhoneNumberConfirmed,
    IReadOnlyList<string> Roles);
