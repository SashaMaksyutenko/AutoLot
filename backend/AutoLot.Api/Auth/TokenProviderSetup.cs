using AutoLot.Domain.Identity;
using Microsoft.AspNetCore.Identity;

namespace AutoLot.Api.Auth;

/// <summary>
/// Провайдери токенів Identity — ті, що видають одноразові коди для
/// відновлення пароля й підтвердження пошти.
///
/// Чому саме тут, а не поруч з рештою налаштувань Identity в Infrastructure:
/// <c>AddDefaultTokenProviders</c> живе у фреймворку ASP.NET Core, і щоб
/// викликати його з бібліотеки, туди довелося б додати посилання на весь
/// вебфреймворк. Це відкрило б Infrastructure доступ до контролерів і
/// SignalR — рівно те, чого ми уникали, виносячи розсилку торгів за
/// інтерфейс. Один виклик у правильному шарі дешевший за розмиту межу.
/// </summary>
internal static class TokenProviderSetup
{
    public static IServiceCollection AddAutoLotTokenProviders(this IServiceCollection services)
    {
        // IdentityBuilder — це просто обгортка навколо колекції сервісів;
        // Identity вже налаштований в Infrastructure, тут ми лише доповнюємо
        // його провайдерами токенів.
        new IdentityBuilder(typeof(User), typeof(Role), services).AddDefaultTokenProviders();

        // Скільки живе посилання з листа. Стандартна доба задовга для листа,
        // який відкриває доступ до зміни пароля: скринька могла лишитися
        // відкритою на чужому комп'ютері.
        services.Configure<DataProtectionTokenProviderOptions>(tokens =>
            tokens.TokenLifespan = TimeSpan.FromHours(2));

        return services;
    }
}
