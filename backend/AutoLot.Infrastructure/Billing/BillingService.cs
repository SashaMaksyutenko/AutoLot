using AutoLot.Application.Billing;
using AutoLot.Application.Billing.Dtos;
using AutoLot.Application.Common.Abstractions;
using AutoLot.Domain.Billing;
using AutoLot.Domain.Common;
using AutoLot.Domain.Enums;
using AutoLot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoLot.Infrastructure.Billing;

/// <summary>
/// Гаманець і підписки.
///
/// Реальних платежів немає: поповнення просто нараховує суму. Це свідоме
/// спрощення з обсягу проєкту, а не заглушка на місці інтеграції — списання
/// ж працює по-справжньому, з перевіркою коштів і записом у історію.
/// </summary>
internal sealed partial class BillingService(
    AutoLotDbContext dbContext,
    IDateTimeProvider clock,
    ICurrentLanguage language,
    ILogger<BillingService> logger) : IBillingService, IListingAllowance
{
    /// <summary>Скільки останніх рухів показуємо в кабінеті.</summary>
    private const int RecentEntries = 20;

    /// <summary>Стеля одного поповнення. Захист від помилки в нулях, не від шахрайства.</summary>
    private const decimal MaxTopUp = 100_000m;

    public async Task<WalletState> GetWalletAsync(
        long userId,
        CancellationToken cancellationToken = default)
    {
        var wallet = await LoadWalletAsync(userId, cancellationToken);

        return await StateOfAsync(wallet, cancellationToken);
    }

    public async Task<WalletState> TopUpAsync(
        long userId,
        decimal amount,
        CancellationToken cancellationToken = default)
    {
        if (amount > MaxTopUp)
        {
            throw new DomainRuleException($"За раз можна поповнити не більше {MaxTopUp:0}.");
        }

        var wallet = await LoadWalletAsync(userId, cancellationToken);

        wallet.Deposit(amount, WalletOperation.TopUp, clock.UtcNow);

        await dbContext.SaveChangesAsync(cancellationToken);

        LogToppedUp(logger, userId, amount, wallet.Balance);

        return await StateOfAsync(wallet, cancellationToken);
    }

    public async Task<IReadOnlyList<PlanCard>> GetPlansAsync(
        long? userId,
        CancellationToken cancellationToken = default)
    {
        var current = userId is { } id
            ? await CurrentPlanIdAsync(id, cancellationToken)
            : null;

        var plans = await LoadPlansAsync(cancellationToken);

        return [.. plans.Select(plan => ToCard(plan, plan.Id == current))];
    }

    public async Task<SubscriptionState> GetSubscriptionAsync(
        long userId,
        CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        var active = await ActiveSubscriptionAsync(userId, now, cancellationToken);
        var plan = active?.Plan ?? await DefaultPlanAsync(cancellationToken);

        return new SubscriptionState(
            ToCard(plan, IsCurrent: true),
            await PaidUntilAsync(userId, now, cancellationToken),
            await CountActiveListingsAsync(userId, cancellationToken));
    }

    public async Task<SubscriptionState> SubscribeAsync(
        long userId,
        string planCode,
        CancellationToken cancellationToken = default)
    {
        var plan = await dbContext.Plans
            .Include(item => item.Translations)
            .FirstOrDefaultAsync(item => item.Code == planCode, cancellationToken)
            ?? throw new PlanNotFoundException(planCode);

        // Безкоштовний тариф не «оформлюють»: він і так діє в кожного, хто
        // нічого не купував. Дозволити купівлю означало б продавати нуль.
        if (plan.IsDefault)
        {
            throw new SubscriptionNotAllowedException(
                "Базовий тариф діє без оформлення — просто дочекайтеся кінця платного.");
        }

        var now = clock.UtcNow;
        var active = await ActiveSubscriptionAsync(userId, now, cancellationToken);

        // Продовжити можна лише той самий план. Перехід на інший, поки діє
        // оплачений, довелося б перераховувати — а це вже тарифікація, якої
        // проєкт свідомо не має.
        if (active is not null && active.PlanId != plan.Id)
        {
            throw new SubscriptionNotAllowedException(
                "Спершу має завершитися чинний тариф — переходи посеред періоду не передбачені.");
        }

        var wallet = await LoadWalletAsync(userId, cancellationToken);

        // Списання й оформлення — в одному SaveChanges. Або сталося все,
        // або нічого: списаних коштів без підписки бути не має.
        wallet.Withdraw(plan.Price, WalletOperation.SubscriptionCharge, now);

        dbContext.Subscriptions.Add(Subscription.Start(userId, plan, now, active?.EndsAt));

        await dbContext.SaveChangesAsync(cancellationToken);

        LogSubscribed(logger, userId, plan.Code, plan.Price);

        return await GetSubscriptionAsync(userId, cancellationToken);
    }

    public async Task<int?> GetListingLimitAsync(
        long userId,
        CancellationToken cancellationToken = default)
    {
        // Дилерський акаунт не обмежений незалежно від тарифу (SPEC §3):
        // вітрина салону без оголошень не має сенсу.
        var isDealer = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => user.AccountType == AccountType.Dealer)
            .FirstOrDefaultAsync(cancellationToken);

        if (isDealer)
        {
            return null;
        }

        var active = await ActiveSubscriptionAsync(userId, clock.UtcNow, cancellationToken);

        if (active is not null)
        {
            return active.Plan.ListingLimit;
        }

        return (await DefaultPlanAsync(cancellationToken)).ListingLimit;
    }

    /// <summary>
    /// Гаманець користувача, створюючи його за потреби. Заводити гаманець
    /// при реєстрації не варто: більшість людей нічого не платитиме, а
    /// порожній рядок на кожного — зайва таблиця розміром із таблицю
    /// користувачів.
    /// </summary>
    private async Task<Wallet> LoadWalletAsync(long userId, CancellationToken cancellationToken)
    {
        var wallet = await dbContext.Wallets
            .FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);

        if (wallet is null)
        {
            wallet = new Wallet { UserId = userId, Balance = 0m };

            dbContext.Wallets.Add(wallet);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return wallet;
    }

    private async Task<WalletState> StateOfAsync(Wallet wallet, CancellationToken cancellationToken)
    {
        var recent = await dbContext.WalletTransactions
            .AsNoTracking()
            .Where(item => item.WalletId == wallet.Id)
            .OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.Id)
            .Take(RecentEntries)
            .Select(item => new WalletEntry(
                item.Id,
                item.Amount,
                item.Kind,
                item.BalanceAfter,
                item.CreatedAt))
            .ToListAsync(cancellationToken);

        return new WalletState(wallet.Balance, recent);
    }

    /// <summary>
    /// Доки оплачено загалом.
    /// </summary>
    /// <remarks>
    /// Це НЕ кінець поточного рядка. Продовження створює наступний період,
    /// який починається від кінця попереднього, тож у момент продовження
    /// чинним лишається старий рядок — і саме його кінець показувався б
    /// людині, яка щойно заплатила за ще місяць. Тому беремо найдальшу межу
    /// серед усіх ще не завершених періодів.
    /// </remarks>
    private async Task<DateTimeOffset?> PaidUntilAsync(
        long userId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var periods = await dbContext.Subscriptions
            .AsNoTracking()
            .Where(item => item.UserId == userId && item.EndsAt > now)
            .Select(item => item.EndsAt)
            .ToListAsync(cancellationToken);

        return periods.Count == 0 ? null : periods.Max();
    }

    /// <summary>Чинна підписка або <c>null</c>, якщо діє безкоштовний тариф.</summary>
    private async Task<Subscription?> ActiveSubscriptionAsync(
        long userId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        return await dbContext.Subscriptions
            .AsNoTracking()
            .Include(item => item.Plan).ThenInclude(plan => plan.Translations)
            .Where(item => item.UserId == userId && item.StartsAt <= now && now < item.EndsAt)
            // Якщо періодів чомусь кілька, чинним вважаємо найдовший:
            // людина заплатила за нього й не має втратити оплачене.
            .OrderByDescending(item => item.EndsAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<long?> CurrentPlanIdAsync(long userId, CancellationToken cancellationToken)
    {
        var active = await ActiveSubscriptionAsync(userId, clock.UtcNow, cancellationToken);

        return active?.PlanId ?? (await DefaultPlanAsync(cancellationToken)).Id;
    }

    /// <summary>
    /// Безкоштовний план. Його відсутність — не порожній результат, а зламаний
    /// сід: без нього незрозуміло, що дозволено людині, яка нічого не купувала.
    /// </summary>
    private async Task<Plan> DefaultPlanAsync(CancellationToken cancellationToken)
    {
        return await dbContext.Plans
            .AsNoTracking()
            .Include(plan => plan.Translations)
            .OrderBy(plan => plan.SortOrder)
            .FirstOrDefaultAsync(plan => plan.IsDefault, cancellationToken)
            ?? throw new InvalidOperationException(
                "У довіднику немає плану за замовчуванням. Перевірте сід plans.json.");
    }

    private async Task<List<Plan>> LoadPlansAsync(CancellationToken cancellationToken)
    {
        return await dbContext.Plans
            .AsNoTracking()
            .Include(plan => plan.Translations)
            .OrderBy(plan => plan.SortOrder)
            .ToListAsync(cancellationToken);
    }

    private async Task<int> CountActiveListingsAsync(long userId, CancellationToken cancellationToken)
    {
        // Рахуємо так само, як перевіряє ліміт: лише особисті оголошення,
        // активні та подані на модерацію. Інакше «3 з 5» у кабінеті
        // розходилося б із відмовою при публікації четвертого.
        return await dbContext.Listings
            .AsNoTracking()
            .CountAsync(
                listing => listing.SellerId == userId
                    && listing.DealershipId == null
                    && (listing.Status == ListingStatus.Active
                        || listing.Status == ListingStatus.PendingModeration),
                cancellationToken);
    }

    private PlanCard ToCard(Plan plan, bool IsCurrent)
    {
        var code = language.Code;

        var translation = plan.Translations.FirstOrDefault(item => item.Language == code)
            ?? plan.Translations.FirstOrDefault(item => item.Language == LanguageCodes.Default);

        return new PlanCard(
            plan.Id,
            plan.Code,
            translation?.Name ?? plan.Code,
            translation?.Description ?? string.Empty,
            plan.Price,
            plan.DurationDays,
            plan.ListingLimit,
            plan.IsDefault,
            IsCurrent);
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Гаманець {UserId} поповнено на {Amount}; баланс {Balance}")]
    private static partial void LogToppedUp(
        ILogger logger,
        long userId,
        decimal amount,
        decimal balance);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Користувач {UserId} оформив тариф {Plan} за {Price}")]
    private static partial void LogSubscribed(
        ILogger logger,
        long userId,
        string plan,
        decimal price);
}
