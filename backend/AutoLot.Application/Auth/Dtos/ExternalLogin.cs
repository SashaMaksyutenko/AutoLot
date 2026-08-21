namespace AutoLot.Application.Auth.Dtos;

/// <summary>Те, що ми дізналися про користувача від зовнішнього провайдера.</summary>
public sealed record ExternalLogin(
    string Provider,
    string ProviderKey,
    string Email,
    string? DisplayName,
    bool EmailVerified);
