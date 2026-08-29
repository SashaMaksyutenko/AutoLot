namespace AutoLot.Application.Auth.Dtos;

/// <summary>Прохання надіслати лист для відновлення пароля.</summary>
public sealed record ForgotPasswordRequest
{
    public string Email { get; init; } = string.Empty;
}

/// <summary>Новий пароль разом із токеном із листа.</summary>
public sealed record ResetPasswordRequest
{
    public string Email { get; init; } = string.Empty;

    public string Token { get; init; } = string.Empty;

    public string NewPassword { get; init; } = string.Empty;
}

/// <summary>Підтвердження пошти за посиланням із листа.</summary>
public sealed record ConfirmEmailRequest
{
    public string Email { get; init; } = string.Empty;

    public string Token { get; init; } = string.Empty;
}
