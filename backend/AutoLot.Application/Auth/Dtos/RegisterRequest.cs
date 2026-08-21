using AutoLot.Domain.Enums;

namespace AutoLot.Application.Auth.Dtos;

public sealed record RegisterRequest(
    string Email,
    string Password,
    string DisplayName,
    AccountType AccountType,
    string? PhoneNumber);
