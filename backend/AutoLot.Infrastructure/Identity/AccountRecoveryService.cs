using AutoLot.Application.Auth;
using AutoLot.Application.Common.Abstractions;
using AutoLot.Domain.Identity;
using AutoLot.Infrastructure.Email;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace AutoLot.Infrastructure.Identity;

/// <summary>
/// Відновлення пароля й підтвердження пошти.
///
/// Наскрізне правило: назовні не видно, чи існує акаунт. Тому запит на
/// відновлення нічого не повертає, а всі гілки — і «знайшли», і «не
/// знайшли» — закінчуються однаково. Інакше форма «забув пароль»
/// перетворилася б на спосіб перевіряти, хто зареєстрований (SPEC §8).
/// </summary>
internal sealed partial class AccountRecoveryService(
    UserManager<User> userManager,
    IEmailSender emailSender,
    AccountEmails emails,
    ILogger<AccountRecoveryService> logger) : IAccountRecoveryService
{
    public async Task RequestPasswordResetAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByEmailAsync(email);

        if (user is null)
        {
            // Пишемо в лог, але клієнту відповідаємо так само, як і при успіху.
            LogResetForUnknown(logger, email);
            return;
        }

        var token = await userManager.GeneratePasswordResetTokenAsync(user);

        await emailSender.SendAsync(emails.PasswordReset(email, token), cancellationToken);

        LogResetSent(logger, user.Id);
    }

    public async Task<bool> ResetPasswordAsync(
        string email,
        string token,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByEmailAsync(email);

        if (user is null)
        {
            return false;
        }

        var result = await userManager.ResetPasswordAsync(user, token, newPassword);

        if (!result.Succeeded)
        {
            LogResetFailed(logger, user.Id, string.Join("; ", result.Errors.Select(e => e.Description)));
            return false;
        }

        // Зміна пароля має вибивати всі відкриті сесії: якщо пароль міняли
        // через те, що його вкрали, чужий refresh-токен мусить перестати
        // працювати негайно.
        await userManager.UpdateSecurityStampAsync(user);

        LogResetDone(logger, user.Id);

        return true;
    }

    public async Task SendEmailConfirmationAsync(
        long userId,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString(System.Globalization.CultureInfo.InvariantCulture));

        if (user?.Email is null || user.EmailConfirmed)
        {
            return;
        }

        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);

        await emailSender.SendAsync(emails.EmailConfirmation(user.Email, token), cancellationToken);

        LogConfirmationSent(logger, user.Id);
    }

    public async Task<bool> ConfirmEmailAsync(
        string email,
        string token,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByEmailAsync(email);

        if (user is null)
        {
            return false;
        }

        // Повторне підтвердження вже підтвердженої пошти — не помилка: людина
        // могла двічі натиснути посилання в листі.
        if (user.EmailConfirmed)
        {
            return true;
        }

        var result = await userManager.ConfirmEmailAsync(user, token);

        if (!result.Succeeded)
        {
            LogConfirmationFailed(logger, user.Id);
            return false;
        }

        LogConfirmed(logger, user.Id);

        return true;
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Надіслано лист відновлення пароля користувачу {UserId}")]
    private static partial void LogResetSent(ILogger logger, long userId);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Запит на відновлення пароля для незареєстрованої адреси {Email}")]
    private static partial void LogResetForUnknown(ILogger logger, string email);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Warning,
        Message = "Не вдалося змінити пароль користувачу {UserId}: {Errors}")]
    private static partial void LogResetFailed(ILogger logger, long userId, string errors);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Information,
        Message = "Пароль користувача {UserId} змінено через відновлення")]
    private static partial void LogResetDone(ILogger logger, long userId);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Information,
        Message = "Надіслано лист підтвердження пошти користувачу {UserId}")]
    private static partial void LogConfirmationSent(ILogger logger, long userId);

    [LoggerMessage(
        EventId = 6,
        Level = LogLevel.Warning,
        Message = "Не вдалося підтвердити пошту користувача {UserId}: токен недійсний")]
    private static partial void LogConfirmationFailed(ILogger logger, long userId);

    [LoggerMessage(
        EventId = 7,
        Level = LogLevel.Information,
        Message = "Пошту користувача {UserId} підтверджено")]
    private static partial void LogConfirmed(ILogger logger, long userId);
}
