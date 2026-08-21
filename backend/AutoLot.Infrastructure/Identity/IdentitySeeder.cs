using AutoLot.Domain.Enums;
using AutoLot.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutoLot.Infrastructure.Identity;

/// <summary>
/// Створює ролі та першого адміністратора (SPEC §3). Ідемпотентний: повторний
/// запуск нічого не дублює й не перезаписує наявного адміністратора.
/// </summary>
public sealed partial class IdentitySeeder(
    RoleManager<Role> roleManager,
    UserManager<User> userManager,
    IOptions<AdminSeedOptions> options,
    ILogger<IdentitySeeder> logger)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await SeedRolesAsync();
        await SeedAdminAsync();
    }

    private async Task SeedRolesAsync()
    {
        foreach (var roleName in RoleNames.All)
        {
            if (await roleManager.RoleExistsAsync(roleName))
            {
                continue;
            }

            var created = await roleManager.CreateAsync(new Role(roleName));

            if (created.Succeeded)
            {
                LogRoleCreated(logger, roleName);
            }
            else
            {
                LogRoleCreationFailed(logger, roleName, Describe(created));
            }
        }
    }

    private async Task SeedAdminAsync()
    {
        var seed = options.Value;

        if (!seed.IsConfigured)
        {
            // Пароля за замовчуванням у коді немає навмисно: адміністратор
            // із загальновідомим паролем гірший, ніж його відсутність.
            LogAdminNotConfigured(logger, AdminSeedOptions.SectionName);
            return;
        }

        var existing = await userManager.FindByEmailAsync(seed.Email!);

        if (existing is not null)
        {
            if (!await userManager.IsInRoleAsync(existing, RoleNames.Admin))
            {
                await userManager.AddToRoleAsync(existing, RoleNames.Admin);
                LogAdminRoleGranted(logger, seed.Email!);
            }

            return;
        }

        var admin = new User
        {
            UserName = seed.Email,
            Email = seed.Email,
            DisplayName = seed.DisplayName,
            AccountType = AccountType.Private,
            EmailConfirmed = true,
        };

        var created = await userManager.CreateAsync(admin, seed.Password!);

        if (!created.Succeeded)
        {
            LogAdminCreationFailed(logger, Describe(created));
            return;
        }

        await userManager.AddToRolesAsync(admin, [RoleNames.Admin, RoleNames.User]);

        LogAdminCreated(logger, seed.Email!);
    }

    private static string Describe(IdentityResult result) =>
        string.Join("; ", result.Errors.Select(error => error.Description));

    [LoggerMessage(Level = LogLevel.Information, Message = "Створено роль {Role}")]
    private static partial void LogRoleCreated(ILogger logger, string role);

    [LoggerMessage(Level = LogLevel.Error, Message = "Не вдалося створити роль {Role}: {Errors}")]
    private static partial void LogRoleCreationFailed(ILogger logger, string role, string errors);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Адміністратора не створено: у конфігурації немає {Section} з Email і Password")]
    private static partial void LogAdminNotConfigured(ILogger logger, string section);

    [LoggerMessage(Level = LogLevel.Information, Message = "Користувачу {Email} видано роль адміністратора")]
    private static partial void LogAdminRoleGranted(ILogger logger, string email);

    [LoggerMessage(Level = LogLevel.Error, Message = "Не вдалося створити адміністратора: {Errors}")]
    private static partial void LogAdminCreationFailed(ILogger logger, string errors);

    [LoggerMessage(Level = LogLevel.Information, Message = "Створено адміністратора {Email}")]
    private static partial void LogAdminCreated(ILogger logger, string email);
}
