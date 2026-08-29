namespace AutoLot.Application.Auth;

/// <summary>
/// Відновлення пароля й підтвердження пошти.
///
/// Головне правило всього цього сценарію: **назовні не видно, чи існує
/// акаунт**. Запит на відновлення відповідає однаково і для зареєстрованої
/// адреси, і для вигаданої — інакше форму «забув пароль» використовували б
/// як перевірку, чи є в майданчика така людина (SPEC §8).
/// </summary>
public interface IAccountRecoveryService
{
    /// <summary>
    /// Надсилає лист із посиланням на зміну пароля. Нічого не повертає
    /// свідомо: відповідь має бути однакова незалежно від того, чи знайшли
    /// таку скриньку.
    /// </summary>
    Task RequestPasswordResetAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>Задає новий пароль за токеном із листа.</summary>
    Task<bool> ResetPasswordAsync(
        string email,
        string token,
        string newPassword,
        CancellationToken cancellationToken = default);

    /// <summary>Надсилає лист із підтвердженням пошти.</summary>
    Task SendEmailConfirmationAsync(long userId, CancellationToken cancellationToken = default);

    /// <summary>Підтверджує пошту за токеном із листа.</summary>
    Task<bool> ConfirmEmailAsync(
        string email,
        string token,
        CancellationToken cancellationToken = default);
}
