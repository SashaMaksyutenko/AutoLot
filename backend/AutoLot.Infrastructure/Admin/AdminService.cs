using AutoLot.Application.Admin;
using AutoLot.Application.Admin.Dtos;
using AutoLot.Application.Common;
using AutoLot.Domain.Enums;
using AutoLot.Domain.Identity;
using AutoLot.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoLot.Infrastructure.Admin;

/// <summary>
/// Керування людьми й ролями. Кожна дія пишеться в лог: за SPEC §8 такі
/// рішення підлягають аудиту, і поки окремого аудит-логу немає, слід має
/// лишатися хоча б тут.
/// </summary>
internal sealed partial class AdminService(
    AutoLotDbContext dbContext,
    UserManager<User> userManager,
    ILogger<AdminService> logger) : IAdminService
{
    private const int MaxPageSize = 100;

    public async Task<PagedResult<UserSummary>> SearchUsersAsync(
        UserSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);

        var users = dbContext.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Text))
        {
            var pattern = $"%{query.Text.Trim()}%";

            // Шукаємо і в імені, і в пошті: адміністратор може знати будь-що
            // з двох, а питати «за чим шукаєте» — зайвий крок.
            users = users.Where(user =>
                EF.Functions.ILike(user.DisplayName, pattern)
                || (user.Email != null && EF.Functions.ILike(user.Email, pattern)));
        }

        if (query.IsBanned is { } banned)
        {
            users = users.Where(user => user.IsBanned == banned);
        }

        if (!string.IsNullOrWhiteSpace(query.Role))
        {
            var role = query.Role;

            // Ролі лежать у таблицях Identity, тож фільтр іде підзапитом.
            users = users.Where(user => dbContext.UserRoles.Any(link =>
                link.UserId == user.Id
                && dbContext.Roles.Any(r => r.Id == link.RoleId && r.Name == role)));
        }

        var totalCount = await users.CountAsync(cancellationToken);

        var items = await users
            .OrderByDescending(user => user.CreatedAt)
            .ThenByDescending(user => user.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(user => new UserSummary(
                user.Id,
                user.DisplayName,
                user.Email ?? string.Empty,
                user.AccountType,
                user.IsBanned,
                user.EmailConfirmed,
                user.CreatedAt,
                user.LastLoginAt,
                dbContext.UserRoles
                    .Where(link => link.UserId == user.Id)
                    .Join(dbContext.Roles, link => link.RoleId, r => r.Id, (_, r) => r.Name ?? string.Empty)
                    .ToList(),
                dbContext.Listings.Count(listing =>
                    listing.SellerId == user.Id && listing.Status == ListingStatus.Active)))
            .ToListAsync(cancellationToken);

        return new PagedResult<UserSummary>(items, page, pageSize, totalCount);
    }

    public async Task SetBannedAsync(
        long userId,
        long adminId,
        bool isBanned,
        CancellationToken cancellationToken = default)
    {
        // Заблокувати себе означало б втратити доступ до адмінки назавжди.
        if (userId == adminId)
        {
            throw new AdminActionException("Заблокувати власний акаунт не можна.");
        }

        var user = await dbContext.Users
            .FirstOrDefaultAsync(item => item.Id == userId, cancellationToken)
            ?? throw new UserNotFoundException(userId);

        user.IsBanned = isBanned;

        await dbContext.SaveChangesAsync(cancellationToken);

        // Печатка безпеки вибиває всі відкриті сесії: заблокований не має
        // дочекатися, поки протухне його поточний токен.
        await userManager.UpdateSecurityStampAsync(user);

        LogBanChanged(logger, userId, adminId, isBanned);
    }

    public async Task SetRoleAsync(
        long userId,
        long adminId,
        string role,
        bool granted,
        CancellationToken cancellationToken = default)
    {
        if (!RoleNames.All.Contains(role))
        {
            throw new AdminActionException($"Ролі «{role}» не існує.");
        }

        // Зняти з себе адміністратора — той самий спосіб замкнути двері
        // зсередини й лишити ключі всередині.
        if (userId == adminId && role == RoleNames.Admin && !granted)
        {
            throw new AdminActionException("Зняти з себе роль адміністратора не можна.");
        }

        var user = await userManager.FindByIdAsync(
            userId.ToString(System.Globalization.CultureInfo.InvariantCulture))
            ?? throw new UserNotFoundException(userId);

        var result = granted
            ? await userManager.AddToRoleAsync(user, role)
            : await userManager.RemoveFromRoleAsync(user, role);

        if (!result.Succeeded)
        {
            throw new AdminActionException(
                string.Join("; ", result.Errors.Select(error => error.Description)));
        }

        // Два різні записи, а не один із прапорцем: аудит читають люди, і
        // «Адміністратор 28 True роль Moderator» — не речення.
        if (granted)
        {
            LogRoleGranted(logger, userId, adminId, role);
        }
        else
        {
            LogRoleRevoked(logger, userId, adminId, role);
        }
    }

    public async Task<PlatformStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        return new PlatformStats(
            await dbContext.Users.CountAsync(cancellationToken),
            await dbContext.Users.CountAsync(user => user.IsBanned, cancellationToken),
            await dbContext.Listings.CountAsync(
                listing => listing.Status == ListingStatus.Active,
                cancellationToken),
            await dbContext.Listings.CountAsync(
                listing => listing.Status == ListingStatus.PendingModeration,
                cancellationToken),
            await dbContext.Auctions.CountAsync(
                auction => auction.Status == AuctionStatus.Active,
                cancellationToken),
            await dbContext.Dealerships.CountAsync(cancellationToken),
            await dbContext.Dealerships.CountAsync(
                dealership => !dealership.IsVerified,
                cancellationToken));
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Адміністратор {AdminId} змінив блокування користувача {UserId} на {IsBanned}")]
    private static partial void LogBanChanged(
        ILogger logger,
        long userId,
        long adminId,
        bool isBanned);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "Адміністратор {AdminId} призначив користувачу {UserId} роль {Role}")]
    private static partial void LogRoleGranted(
        ILogger logger,
        long userId,
        long adminId,
        string role);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Warning,
        Message = "Адміністратор {AdminId} зняв з користувача {UserId} роль {Role}")]
    private static partial void LogRoleRevoked(
        ILogger logger,
        long userId,
        long adminId,
        string role);
}
