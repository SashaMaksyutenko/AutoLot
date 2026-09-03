using AutoLot.Domain.Common;
using AutoLot.Domain.Identity;

namespace AutoLot.Domain.Billing;

/// <summary>
/// Віртуальний баланс користувача (SPEC §3).
///
/// **Справжніх грошей тут немає й не буде** — платіжні системи прямо
/// виключені з обсягу проєкту. Це умовні одиниці, якими оплачують підписку
/// й під які резервують депозит учасника торгів.
///
/// Чому баланс окремою сутністю, а не полем у користувача: у нього своє
/// життя — рух коштів, історія, і згодом блокування під депозит. Поле в
/// таблиці користувачів перетворило б кожне списання на оновлення рядка,
/// який читають усюди.
/// </summary>
public sealed class Wallet : Entity
{
    public long UserId { get; set; }

    public User User { get; set; } = null!;

    /// <summary>
    /// Скільки на рахунку. Зберігається числом, а не рахується з історії
    /// щоразу: історія росте без межі, а баланс потрібен на кожній сторінці.
    /// Записи руху при цьому лишаються — за ними баланс завжди можна
    /// перерахувати й звірити.
    /// </summary>
    public decimal Balance { get; set; }

    public ICollection<WalletTransaction> Transactions { get; } = [];

    /// <summary>Поповнення. У демо гроші беруться нізвідки — платежів немає.</summary>
    public WalletTransaction Deposit(decimal amount, WalletOperation kind, DateTimeOffset now)
    {
        if (amount <= 0)
        {
            throw new DomainRuleException("Сума поповнення має бути додатною.");
        }

        Balance += amount;

        return Record(amount, kind, now);
    }

    /// <summary>
    /// Списання. Кидає, якщо коштів не вистачає: піти в мінус віртуальний
    /// баланс не може — інакше «оплата» перестала б щось означати.
    /// </summary>
    public WalletTransaction Withdraw(decimal amount, WalletOperation kind, DateTimeOffset now)
    {
        if (amount <= 0)
        {
            throw new DomainRuleException("Сума списання має бути додатною.");
        }

        if (Balance < amount)
        {
            throw new InsufficientFundsException(amount, Balance);
        }

        Balance -= amount;

        return Record(-amount, kind, now);
    }

    /// <summary>
    /// Записує рух. Зберігаємо ще й баланс ПІСЛЯ операції: без нього історія
    /// відповідає на «скільки списали», але не на «скільки лишалося», а саме
    /// друге питання виникає, коли щось не сходиться.
    /// </summary>
    private WalletTransaction Record(decimal amount, WalletOperation kind, DateTimeOffset now)
    {
        var transaction = new WalletTransaction
        {
            WalletId = Id,
            Wallet = this,
            Amount = amount,
            Kind = kind,
            BalanceAfter = Balance,
            CreatedAt = now,
        };

        Transactions.Add(transaction);

        return transaction;
    }
}

/// <summary>Коштів на рахунку не вистачає.</summary>
public sealed class InsufficientFundsException(decimal required, decimal available)
    : Exception($"На балансі {available:0.00}, а потрібно {required:0.00}.")
{
    public decimal Required { get; } = required;

    public decimal Available { get; } = available;
}
