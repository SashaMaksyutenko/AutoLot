using AutoLot.Application.Auth;
using AutoLot.Application.Auth.Dtos;
using AutoLot.Application.Common.Abstractions;
using AutoLot.Domain.Enums;
using AutoLot.Domain.Identity;
using AutoLot.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutoLot.Infrastructure.Identity;

internal sealed class AuthService(
    UserManager<User> userManager,
    AutoLotDbContext dbContext,
    JwtTokenGenerator tokenGenerator,
    IOptions<JwtOptions> jwtOptions,
    IDateTimeProvider clock,
    ILogger<AuthService> logger) : IAuthService
{
    /// <summary>
    /// Однаковий текст і для «немає такого email», і для «невірний пароль» —
    /// щоб форма входу не підказувала, хто в нас зареєстрований.
    /// </summary>
    private const string InvalidCredentialsMessage = "Невірний email або пароль.";

    public async Task<AuthResult> RegisterAsync(
        RegisterRequest request,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = new User
        {
            UserName = request.Email,
            Email = request.Email,
            DisplayName = request.DisplayName.Trim(),
            AccountType = request.AccountType,
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber,
        };

        var created = await userManager.CreateAsync(user, request.Password);

        if (!created.Succeeded)
        {
            var duplicate = created.Errors.Any(error =>
                error.Code is "DuplicateEmail" or "DuplicateUserName");

            return duplicate
                ? AuthResult.Failure(AuthError.EmailAlreadyUsed, "Такий email уже зареєстровано.")
                : AuthResult.Failure(
                    AuthError.PasswordRejected,
                    [.. created.Errors.Select(error => error.Description)]);
        }

        var roleAssigned = await userManager.AddToRoleAsync(user, RoleNames.User);

        if (!roleAssigned.Succeeded)
        {
            // Найімовірніша причина — ролі не засіяні. Реєстрацію не валимо,
            // але слід у логах лишаємо: права такого користувача будуть неповні.
            logger.LogError(
                "Не вдалося призначити роль {Role} користувачу {UserId}: {Errors}",
                RoleNames.User,
                user.Id,
                string.Join("; ", roleAssigned.Errors.Select(error => error.Description)));
        }

        return AuthResult.Success(await IssueTokensAsync(user, familyId: null, ipAddress, cancellationToken));
    }

    public async Task<AuthResult> LoginAsync(
        LoginRequest request,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = await userManager.FindByEmailAsync(request.Email);

        if (user is null)
        {
            return AuthResult.Failure(AuthError.InvalidCredentials, InvalidCredentialsMessage);
        }

        if (user.IsBanned)
        {
            return AuthResult.Failure(AuthError.AccountBanned, "Акаунт заблоковано модератором.");
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            return AuthResult.Failure(
                AuthError.AccountLockedOut,
                "Забагато невдалих спроб. Спробуйте пізніше.");
        }

        if (!await userManager.CheckPasswordAsync(user, request.Password))
        {
            await userManager.AccessFailedAsync(user);
            return AuthResult.Failure(AuthError.InvalidCredentials, InvalidCredentialsMessage);
        }

        await userManager.ResetAccessFailedCountAsync(user);

        user.LastLoginAt = clock.UtcNow;
        await userManager.UpdateAsync(user);

        return AuthResult.Success(await IssueTokensAsync(user, familyId: null, ipAddress, cancellationToken));
    }

    public async Task<AuthResult> RefreshAsync(
        string refreshToken,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return AuthResult.Failure(AuthError.InvalidRefreshToken, "Refresh-токен відсутній.");
        }

        var hash = RefreshTokenFactory.Hash(refreshToken);

        var stored = await dbContext.RefreshTokens
            .Include(token => token.User)
            .FirstOrDefaultAsync(token => token.TokenHash == hash, cancellationToken);

        if (stored is null)
        {
            return AuthResult.Failure(AuthError.InvalidRefreshToken, "Refresh-токен недійсний.");
        }

        var now = clock.UtcNow;

        if (stored.IsRevoked)
        {
            // Погашений токен пред'явили вдруге. Найімовірніше, його вкрали:
            // гасимо всю сім'ю, щоб і зловмисник, і власник входили заново.
            logger.LogWarning(
                "Повторне використання refresh-токена сім'ї {FamilyId} користувача {UserId} з {Ip}",
                stored.FamilyId,
                stored.UserId,
                ipAddress);

            await RevokeFamilyAsync(
                stored.UserId,
                stored.FamilyId,
                "Повторне використання погашеного токена",
                now,
                cancellationToken);

            return AuthResult.Failure(
                AuthError.InvalidRefreshToken,
                "Сесію завершено з міркувань безпеки.");
        }

        if (stored.IsExpired(now))
        {
            return AuthResult.Failure(AuthError.InvalidRefreshToken, "Термін дії сесії минув.");
        }

        if (stored.User.IsBanned)
        {
            return AuthResult.Failure(AuthError.AccountBanned, "Акаунт заблоковано модератором.");
        }

        stored.RevokedAt = now;
        stored.RevokedReason = "Ротація";

        return AuthResult.Success(
            await IssueTokensAsync(stored.User, stored.FamilyId, ipAddress, cancellationToken));
    }

    public async Task RevokeAsync(
        string refreshToken,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        var hash = RefreshTokenFactory.Hash(refreshToken);

        var stored = await dbContext.RefreshTokens
            .FirstOrDefaultAsync(token => token.TokenHash == hash, cancellationToken);

        if (stored is null)
        {
            return;
        }

        await RevokeFamilyAsync(stored.UserId, stored.FamilyId, "Вихід", clock.UtcNow, cancellationToken);
    }

    public async Task<AuthResult> SignInWithExternalAsync(
        ExternalLogin login,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(login);

        // Без підтвердженого email прив'язка до наявного акаунта означала б
        // захоплення чужого профілю тим, хто просто вписав його адресу.
        if (!login.EmailVerified)
        {
            return AuthResult.Failure(AuthError.ExternalLoginFailed, "Провайдер не підтвердив email.");
        }

        var user = await userManager.FindByLoginAsync(login.Provider, login.ProviderKey);

        if (user is null)
        {
            user = await userManager.FindByEmailAsync(login.Email);

            if (user is null)
            {
                user = new User
                {
                    UserName = login.Email,
                    Email = login.Email,
                    DisplayName = string.IsNullOrWhiteSpace(login.DisplayName)
                        ? login.Email
                        : login.DisplayName,
                    AccountType = AccountType.Private,
                    EmailConfirmed = true,
                };

                var created = await userManager.CreateAsync(user);

                if (!created.Succeeded)
                {
                    return AuthResult.Failure(
                        AuthError.ExternalLoginFailed,
                        [.. created.Errors.Select(error => error.Description)]);
                }

                await userManager.AddToRoleAsync(user, RoleNames.User);
            }

            var linked = await userManager.AddLoginAsync(
                user,
                new UserLoginInfo(login.Provider, login.ProviderKey, login.Provider));

            if (!linked.Succeeded)
            {
                return AuthResult.Failure(
                    AuthError.ExternalLoginFailed,
                    [.. linked.Errors.Select(error => error.Description)]);
            }
        }

        if (user.IsBanned)
        {
            return AuthResult.Failure(AuthError.AccountBanned, "Акаунт заблоковано модератором.");
        }

        user.LastLoginAt = clock.UtcNow;
        await userManager.UpdateAsync(user);

        return AuthResult.Success(await IssueTokensAsync(user, familyId: null, ipAddress, cancellationToken));
    }

    public async Task<UserProfile?> GetProfileAsync(
        long userId,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.Users
            .FirstOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);

        return user is null ? null : MapProfile(user, await userManager.GetRolesAsync(user));
    }

    private async Task<AuthTokens> IssueTokensAsync(
        User user,
        Guid? familyId,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var roles = await userManager.GetRolesAsync(user);
        var (accessToken, accessTokenExpiresAt) = tokenGenerator.Create(user, roles);

        var value = RefreshTokenFactory.Generate();
        var now = clock.UtcNow;

        var refreshToken = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = RefreshTokenFactory.Hash(value),
            FamilyId = familyId ?? Guid.NewGuid(),
            CreatedAt = now,
            ExpiresAt = now.AddDays(jwtOptions.Value.RefreshTokenDays),
            CreatedByIp = ipAddress,
        };

        dbContext.RefreshTokens.Add(refreshToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new AuthTokens(
            accessToken,
            accessTokenExpiresAt,
            value,
            refreshToken.ExpiresAt,
            MapProfile(user, roles));
    }

    private async Task RevokeFamilyAsync(
        long userId,
        Guid familyId,
        string reason,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await dbContext.RefreshTokens
            .Where(token => token.UserId == userId
                && token.FamilyId == familyId
                && token.RevokedAt == null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(token => token.RevokedAt, now)
                    .SetProperty(token => token.RevokedReason, reason),
                cancellationToken);
    }

    private static UserProfile MapProfile(User user, IList<string> roles) =>
        new(
            user.Id,
            user.Email ?? string.Empty,
            user.DisplayName,
            user.AccountType,
            user.EmailConfirmed,
            user.PhoneNumberConfirmed,
            [.. roles]);
}
