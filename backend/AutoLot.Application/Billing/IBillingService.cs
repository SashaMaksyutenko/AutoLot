using AutoLot.Application.Billing.Dtos;

namespace AutoLot.Application.Billing;

/// <summary>
/// Гаманець і тарифні плани.
///
/// Один сервіс на дві теми навмисно: підписка **оплачується з гаманця**, і
/// ця операція має бути неподільною — списали й оформили, або нічого. Розвівши
/// їх по двох сервісах, ми отримали б спокусу викликати одне з одного й
/// колись отримати списання без підписки.
///
/// Справжніх платежів у проєкті немає (SPEC «Не входить») — усе живе на
/// віртуальних одиницях.
/// </summary>
public interface IBillingService
{
    /// <summary>Баланс і останні рухи. Гаманець створюється при першому зверненні.</summary>
    Task<WalletState> GetWalletAsync(long userId, CancellationToken cancellationToken = default);

    /// <summary>Поповнення. У демо гроші беруться нізвідки.</summary>
    Task<WalletState> TopUpAsync(
        long userId,
        decimal amount,
        CancellationToken cancellationToken = default);

    /// <summary>Усі плани з позначкою, який діє зараз.</summary>
    Task<IReadOnlyList<PlanCard>> GetPlansAsync(
        long? userId,
        CancellationToken cancellationToken = default);

    /// <summary>Що зараз має користувач і скільки з ліміту вже використано.</summary>
    Task<SubscriptionState> GetSubscriptionAsync(
        long userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Оформлює план, списавши його вартість. Продовження того самого плану
    /// додається до вже оплаченого строку, а не починається заново.
    /// </summary>
    Task<SubscriptionState> SubscribeAsync(
        long userId,
        string planCode,
        CancellationToken cancellationToken = default);
}

/// <summary>Такого тарифного плану немає.</summary>
public sealed class PlanNotFoundException(string code)
    : Exception($"Тарифного плану «{code}» не існує.");

/// <summary>Оформити цей план зараз не можна.</summary>
public sealed class SubscriptionNotAllowedException(string message) : Exception(message);
