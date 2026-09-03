using AutoLot.Domain.Billing;

namespace AutoLot.Application.Billing.Dtos;

/// <summary>Стан гаманця: скільки є і що з ним відбувалося.</summary>
public sealed record WalletState(decimal Balance, IReadOnlyList<WalletEntry> Recent);

/// <summary>Один рядок історії руху коштів.</summary>
public sealed record WalletEntry(
    long Id,

    /// <summary>Зі знаком: додатна — надходження, від'ємна — списання.</summary>
    decimal Amount,
    WalletOperation Kind,
    decimal BalanceAfter,
    DateTimeOffset CreatedAt);

/// <summary>Поповнення. Реальних платежів немає — сума просто нараховується.</summary>
public sealed record TopUpRequest
{
    public decimal Amount { get; init; }
}

/// <summary>Тарифний план для сторінки тарифів.</summary>
public sealed record PlanCard(
    long Id,
    string Code,
    string Name,
    string Description,
    decimal Price,
    int DurationDays,

    /// <summary><c>null</c> — без обмеження.</summary>
    int? ListingLimit,
    bool IsDefault,

    /// <summary>Чи діє цей план у того, хто дивиться, просто зараз.</summary>
    bool IsCurrent);

/// <summary>Що зараз має користувач.</summary>
public sealed record SubscriptionState(
    /// <summary>Чинний план — завжди є: без оплати діє безкоштовний.</summary>
    PlanCard Plan,

    /// <summary>Доки оплачено. Порожнє в безкоштовного плану — він безстроковий.</summary>
    DateTimeOffset? ActiveUntil,

    /// <summary>Скільки активних оголошень уже є — щоб показати «3 з 5».</summary>
    int ActiveListings);
