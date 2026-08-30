namespace AutoLot.Application.Users.Dtos;

/// <summary>
/// Те, що людина може змінити про себе сама.
///
/// Пошти тут немає навмисно: зміна адреси — окремий сценарій із
/// підтвердженням нової скриньки, інакше нею можна було б перехопити чужий
/// акаунт через відновлення пароля.
/// </summary>
public sealed record UpdateProfileRequest
{
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Порожнє означає «прибрати телефон», а не «не змінювати».</summary>
    public string? PhoneNumber { get; init; }
}
