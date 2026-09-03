using AutoLot.Domain.Common;

namespace AutoLot.Domain.Billing;

/// <summary>За що рухалися кошти.</summary>
public enum WalletOperation
{
    /// <summary>Поповнення. У демо — просто нарахування, платежів немає.</summary>
    TopUp = 0,

    /// <summary>Оплата тарифного плану.</summary>
    SubscriptionCharge = 1,

    /// <summary>Повернення — наприклад, якщо підписку не вдалося оформити.</summary>
    Refund = 2,
}

/// <summary>
/// Один рух коштів.
///
/// Записи не редагуються й не видаляються — у цьому весь сенс історії. Якщо
/// операцію треба скасувати, з'являється зустрічний запис, а не правка
/// старого: інакше баланс і його пояснення розійшлися б, і довести, звідки
/// взялася цифра, стало б неможливо.
/// </summary>
public sealed class WalletTransaction : Entity
{
    public long WalletId { get; set; }

    public Wallet Wallet { get; set; } = null!;

    /// <summary>
    /// Сума зі знаком: додатна — надходження, від'ємна — списання. Один
    /// стовпець замість пари «сума + напрямок»: так суму руху за період
    /// рахує звичайний SUM, а не умовний вираз.
    /// </summary>
    public decimal Amount { get; set; }

    public WalletOperation Kind { get; set; }

    /// <summary>Скільки лишилося на рахунку одразу після цієї операції.</summary>
    public decimal BalanceAfter { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
