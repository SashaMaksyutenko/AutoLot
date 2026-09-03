using AutoLot.Domain.Common;
using AutoLot.Domain.Identity;

namespace AutoLot.Domain.Billing;

/// <summary>
/// Оплачений період дії тарифного плану.
///
/// Кожна оплата — окремий рядок, а не оновлення дати в одному. Так видно всю
/// історію: коли людина вперше купила тариф, чи були перерви, скільки разів
/// продовжувала. Один рядок із полем «діє до» відповідав би лише на питання
/// «чи діє зараз», і то без пояснення, чому саме до цієї дати.
/// </summary>
public sealed class Subscription : Entity
{
    public long UserId { get; set; }

    public User User { get; set; } = null!;

    public long PlanId { get; set; }

    public Plan Plan { get; set; } = null!;

    public DateTimeOffset StartsAt { get; set; }

    public DateTimeOffset EndsAt { get; set; }

    /// <summary>
    /// Скільки заплатили насправді. Копія ціни плану на момент купівлі:
    /// тариф може подорожчати, а вже оплачений період має лишатися чесним
    /// свідченням про те, за скільки його брали.
    /// </summary>
    public decimal PricePaid { get; set; }

    public bool IsActiveAt(DateTimeOffset moment) => StartsAt <= moment && moment < EndsAt;

    /// <summary>
    /// Створює оплачений період. Продовження починається не «зараз», а від
    /// кінця попереднього — інакше той, хто продовжив завчасно, втрачав би
    /// залишок оплаченого.
    /// </summary>
    public static Subscription Start(
        long userId,
        Plan plan,
        DateTimeOffset now,
        DateTimeOffset? continueFrom = null)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var startsAt = continueFrom is { } from && from > now ? from : now;

        return new Subscription
        {
            UserId = userId,
            PlanId = plan.Id,
            StartsAt = startsAt,
            EndsAt = startsAt.AddDays(plan.DurationDays),
            PricePaid = plan.Price,
        };
    }
}
