using AutoLot.Application.Admin.Dtos;
using AutoLot.Application.Common;

namespace AutoLot.Application.Admin;

/// <summary>
/// Керування майданчиком: люди, ролі, показники.
///
/// Межа з модерацією проста. Модератор працює з ОГОЛОШЕННЯМИ — черга,
/// схвалення, відмови; це <c>IModerationService</c>. Адміністратор працює з
/// ЛЮДЬМИ — блокування, призначення ролей; це тут. Тому й ролі різні:
/// модератор сюди не потрапляє.
/// </summary>
public interface IAdminService
{
    /// <summary>Пошук людей за іменем або поштою, найновіші першими.</summary>
    Task<PagedResult<UserSummary>> SearchUsersAsync(
        UserSearchQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Блокує або розблоковує акаунт. Заблокований не входить і не поновлює
    /// сесію — його активні токени перестають діяти.
    /// </summary>
    Task SetBannedAsync(
        long userId,
        long adminId,
        bool isBanned,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Призначає або знімає роль. Саме так з'являються модератори — інакше
    /// кожен новий вимагав би зміни конфігурації й перезапуску сервера.
    /// </summary>
    Task SetRoleAsync(
        long userId,
        long adminId,
        string role,
        bool granted,
        CancellationToken cancellationToken = default);

    /// <summary>Показники майданчика для головної сторінки адмінки.</summary>
    Task<PlatformStats> GetStatsAsync(CancellationToken cancellationToken = default);
}

/// <summary>Такого користувача немає.</summary>
public sealed class UserNotFoundException(long userId)
    : Exception($"Користувача {userId} не знайдено.");

/// <summary>Дію заборонено правилами адміністрування.</summary>
public sealed class AdminActionException(string message) : Exception(message);
