using AutoLot.Domain.Billing;
using AutoLot.Domain.Common;

namespace AutoLot.Tests.Billing;

/// <summary>
/// Правила гаманця — без бази. Тут перевіряється те, що має лишатися
/// правдою незалежно від сховища: баланс не йде в мінус, а кожен рух
/// лишає слід.
/// </summary>
public class WalletTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 2, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_new_wallet_is_empty()
    {
        var wallet = new Wallet();

        Assert.Equal(0m, wallet.Balance);
        Assert.Empty(wallet.Transactions);
    }

    [Fact]
    public void A_deposit_raises_the_balance_and_leaves_a_trace()
    {
        var wallet = new Wallet();

        var entry = wallet.Deposit(500m, WalletOperation.TopUp, Now);

        Assert.Equal(500m, wallet.Balance);
        Assert.Equal(500m, entry.Amount);
        Assert.Equal(500m, entry.BalanceAfter);
        Assert.Equal(Now, entry.CreatedAt);
        Assert.Single(wallet.Transactions);
    }

    [Fact]
    public void A_withdrawal_is_recorded_with_a_minus()
    {
        var wallet = new Wallet();
        wallet.Deposit(500m, WalletOperation.TopUp, Now);

        var entry = wallet.Withdraw(149m, WalletOperation.SubscriptionCharge, Now);

        Assert.Equal(351m, wallet.Balance);

        // Один стовпець зі знаком замість пари «сума + напрямок»: так суму
        // руху за період рахує звичайний SUM.
        Assert.Equal(-149m, entry.Amount);
        Assert.Equal(351m, entry.BalanceAfter);
    }

    [Fact]
    public void The_balance_never_goes_negative()
    {
        var wallet = new Wallet();
        wallet.Deposit(100m, WalletOperation.TopUp, Now);

        var refused = Assert.Throws<InsufficientFundsException>(
            () => wallet.Withdraw(149m, WalletOperation.SubscriptionCharge, Now));

        Assert.Equal(149m, refused.Required);
        Assert.Equal(100m, refused.Available);

        // Невдале списання не має лишати ні сліду, ні зміни балансу.
        Assert.Equal(100m, wallet.Balance);
        Assert.Single(wallet.Transactions);
    }

    [Fact]
    public void Spending_everything_is_allowed()
    {
        var wallet = new Wallet();
        wallet.Deposit(149m, WalletOperation.TopUp, Now);

        wallet.Withdraw(149m, WalletOperation.SubscriptionCharge, Now);

        // Рівно нуль — це не мінус.
        Assert.Equal(0m, wallet.Balance);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Amounts_must_be_positive(decimal amount)
    {
        var wallet = new Wallet();

        Assert.Throws<DomainRuleException>(
            () => wallet.Deposit(amount, WalletOperation.TopUp, Now));

        Assert.Throws<DomainRuleException>(
            () => wallet.Withdraw(amount, WalletOperation.TopUp, Now));
    }

    [Fact]
    public void The_history_keeps_every_step()
    {
        var wallet = new Wallet();

        wallet.Deposit(500m, WalletOperation.TopUp, Now);
        wallet.Withdraw(149m, WalletOperation.SubscriptionCharge, Now.AddMinutes(1));
        wallet.Deposit(149m, WalletOperation.Refund, Now.AddMinutes(2));

        // Записи не редагуються й не видаляються: скасування — це зустрічний
        // запис, а не правка старого.
        Assert.Equal(3, wallet.Transactions.Count);
        Assert.Equal([500m, 351m, 500m], wallet.Transactions.Select(item => item.BalanceAfter));
    }
}

/// <summary>Правила оплаченого періоду.</summary>
public class SubscriptionTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 2, 12, 0, 0, TimeSpan.Zero);

    private static Plan Plan(decimal price = 149m, int days = 30) =>
        new() { Id = 2, Code = "plus", Price = price, DurationDays = days, ListingLimit = 20 };

    [Fact]
    public void A_fresh_subscription_starts_now()
    {
        var subscription = Subscription.Start(1, Plan(), Now);

        Assert.Equal(Now, subscription.StartsAt);
        Assert.Equal(Now.AddDays(30), subscription.EndsAt);
        Assert.Equal(149m, subscription.PricePaid);
        Assert.True(subscription.IsActiveAt(Now));
    }

    [Fact]
    public void Renewal_continues_from_the_paid_end_not_from_today()
    {
        var until = Now.AddDays(10);

        var renewed = Subscription.Start(1, Plan(), Now, continueFrom: until);

        // Інакше той, хто продовжив завчасно, втрачав би десять оплачених днів.
        Assert.Equal(until, renewed.StartsAt);
        Assert.Equal(until.AddDays(30), renewed.EndsAt);
    }

    [Fact]
    public void An_expired_period_does_not_push_the_start_backwards()
    {
        var expired = Now.AddDays(-5);

        var fresh = Subscription.Start(1, Plan(), Now, continueFrom: expired);

        Assert.Equal(Now, fresh.StartsAt);
    }

    [Fact]
    public void The_price_paid_is_frozen_at_purchase_time()
    {
        var plan = Plan(price: 149m);
        var subscription = Subscription.Start(1, plan, Now);

        // Тариф подорожчав уже після купівлі.
        plan.Price = 299m;

        Assert.Equal(149m, subscription.PricePaid);
    }

    [Fact]
    public void The_last_moment_of_the_period_is_outside_it()
    {
        var subscription = Subscription.Start(1, Plan(), Now);

        Assert.True(subscription.IsActiveAt(subscription.EndsAt.AddSeconds(-1)));

        // Межа виключна: інакше два періоди поспіль перекривалися б на мить.
        Assert.False(subscription.IsActiveAt(subscription.EndsAt));
    }
}
