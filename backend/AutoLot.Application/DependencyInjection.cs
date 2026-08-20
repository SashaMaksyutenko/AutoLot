using Microsoft.Extensions.DependencyInjection;

namespace AutoLot.Application;

/// <summary>
/// Точка входу для реєстрації прикладного шару. Поки що порожня —
/// сервіси й валідатори додаються в міру появи сценаріїв.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        return services;
    }
}
