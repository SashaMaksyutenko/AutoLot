using AutoLot.Domain.Common;
using AutoLot.Domain.Enums;
using AutoLot.Domain.Identity;
using AutoLot.Domain.Listings;

namespace AutoLot.Domain.Auctions;

/// <summary>
/// Торги за одним лотом. Окрема сутність, а не поля в оголошенні: саме її
/// рядок блокується під час ставки (SPEC §5), і тримати в ньому лише те, що
/// стосується торгів, набагато безпечніше, ніж блокувати все оголошення.
///
/// Правила автоставки живуть тут, у <see cref="PlaceBid"/>, а не в сервісі.
/// Так їх можна перевірити тестами без бази — потрібен лише об'єкт у пам'яті.
/// </summary>
public sealed class Auction : AuditableEntity
{
    public long ListingId { get; set; }

    public Listing Listing { get; set; } = null!;

    /// <summary>Валюта торгів. Змінити її посеред аукціону не можна.</summary>
    public Currency Currency { get; set; }

    /// <summary>З якої суми починаються торги.</summary>
    public decimal StartPrice { get; set; }

    /// <summary>
    /// Нижня межа, за якою продавець згоден віддати авто. Не розкривається
    /// нікому: покупці бачать лише бейдж «резерв не досягнуто» (SPEC §4).
    /// null означає лот без резерву — це перевага, і ми показуємо її явно.
    /// </summary>
    public decimal? ReservePrice { get; set; }

    /// <summary>Ціна, яку бачать усі просто зараз.</summary>
    public decimal CurrentPrice { get; set; }

    public long? LeaderId { get; set; }

    public User? Leader { get; set; }

    /// <summary>
    /// Стеля автоставки лідера — його таємниця. Зберігається тут, а не
    /// вираховується зі списку ставок, свідомо: під час торгів ми вже тримаємо
    /// блокування на цьому рядку, і зайвий запит до таблиці ставок означав би
    /// довше блокування для всіх інших учасників.
    /// </summary>
    public decimal? LeaderMaxAmount { get; set; }

    /// <summary>Скільки ставок видно в історії. Рахуємо тут, щоб не рахувати щоразу запитом.</summary>
    public int BidCount { get; set; }

    public DateTimeOffset StartsAt { get; set; }

    /// <summary>Час завершення. Ставка під кінець торгів відсуває його далі.</summary>
    public DateTimeOffset EndsAt { get; set; }

    public AuctionStatus Status { get; set; } = AuctionStatus.Active;

    /// <summary>
    /// Хто забрав лот. Заповнюється лише при закритті й лише якщо резерв
    /// узято: лідер за ставками й переможець — це різні речі.
    /// </summary>
    public long? WinnerId { get; set; }

    public User? Winner { get; set; }

    public ICollection<Bid> Bids { get; } = [];

    /// <summary>Чи взагалі був хтось охочий.</summary>
    public bool HasBids => LeaderId is not null;

    /// <summary>
    /// Чи дотягнули торги до суми, за якої продавець згоден віддати авто.
    /// Лот без резерву вважається досягнутим одразу.
    /// </summary>
    public bool IsReserveMet => ReservePrice is not { } reserve || CurrentPrice >= reserve;

    /// <summary>Найменша сума, яку зараз приймуть.</summary>
    public decimal MinimumNextBid => BidStep.MinimumNextBid(CurrentPrice, HasBids, Currency);

    /// <summary>
    /// Ставка з автоматичним підвищенням. Учасник називає СТЕЛЮ — найбільше,
    /// що він готовий заплатити, — а система ставить рівно стільки, скільки
    /// потрібно для лідерства, і піднімає ставку сама, коли його перебивають.
    ///
    /// Повертає рядки, які треба додати в історію: їх може бути два, бо
    /// чужа автоставка відбивається тим самим рухом.
    /// </summary>
    /// <param name="bidderId">Хто ставить.</param>
    /// <param name="maxAmount">Його стеля.</param>
    /// <param name="now">Час сервера — єдине джерело істини (SPEC §5).</param>
    /// <param name="extension">На скільки продовжити торги, якщо ставка прийшла під кінець.</param>
    public IReadOnlyList<Bid> PlaceBid(
        long bidderId,
        decimal maxAmount,
        DateTimeOffset now,
        TimeSpan extension)
    {
        EnsureRunning(now);

        // Лідер, який лише піднімає власну стелю, нічого не змінює для інших:
        // ціна та сама, історія та сама. Тому окрема гілка й порожній список.
        if (LeaderId == bidderId)
        {
            return RaiseOwnCeiling(maxAmount, now, extension);
        }

        var minimum = MinimumNextBid;

        if (maxAmount < minimum)
        {
            throw new DomainRuleException(
                $"Ставка має бути щонайменше {minimum:0.##} — це поточна ціна плюс крок.");
        }

        var bids = HasBids ? Challenge(bidderId, maxAmount, now) : OpenBidding(bidderId, maxAmount, now);

        BidCount += bids.Count;
        ExtendIfClosing(now, extension);

        return bids;
    }

    /// <summary>Перша ставка на лоті: платити більше за стартову ціну немає потреби.</summary>
    private List<Bid> OpenBidding(long bidderId, decimal maxAmount, DateTimeOffset now)
    {
        CurrentPrice = StartPrice;
        LeaderId = bidderId;
        LeaderMaxAmount = maxAmount;

        return [NewBid(bidderId, StartPrice, maxAmount, isAutomatic: false, now)];
    }

    /// <summary>
    /// Хтось намагається перебити лідера. Тут вирішується головне питання
    /// автоставки: чия стеля вища. Сама названа сума ролі не грає — важить
    /// лише те, до якої межі кожен готовий дійти.
    /// </summary>
    private List<Bid> Challenge(long bidderId, decimal maxAmount, DateTimeOffset now)
    {
        var leaderId = LeaderId!.Value;
        var leaderMax = LeaderMaxAmount!.Value;

        if (maxAmount > leaderMax)
        {
            // Претендент перемагає. Спершу в історію лягає автоставка лідера,
            // який дійшов до своєї стелі й здався, — інакше виглядало б, ніби
            // ціна стрибнула сама собою.
            var bids = new List<Bid>();

            if (leaderMax > CurrentPrice)
            {
                bids.Add(NewBid(leaderId, leaderMax, maxAmount: null, isAutomatic: true, now));
            }

            // Переплачувати не треба: досить перебити чужу стелю на один крок.
            var winning = Math.Min(maxAmount, leaderMax + BidStep.For(leaderMax, Currency));

            bids.Add(NewBid(bidderId, winning, maxAmount, isAutomatic: false, now));

            CurrentPrice = winning;
            LeaderId = bidderId;
            LeaderMaxAmount = maxAmount;

            return bids;
        }

        // Стеля претендента не вища — лідер утримується. За РІВНИХ стель
        // виграє той, хто виставив свою раніше, тобто чинний лідер (SPEC §4).
        var challengerBid = NewBid(bidderId, maxAmount, maxAmount, isAutomatic: false, now);

        // Ціна піднімається рівно настільки, щоб лідер знову був попереду.
        var defended = Math.Min(maxAmount + BidStep.For(maxAmount, Currency), leaderMax);

        var result = new List<Bid> { challengerBid };

        if (defended > CurrentPrice)
        {
            result.Add(NewBid(leaderId, defended, maxAmount: null, isAutomatic: true, now));
            CurrentPrice = defended;
        }
        else
        {
            CurrentPrice = maxAmount;
        }

        return result;
    }

    /// <summary>
    /// Лідер піднімає власну стелю. Публічно не змінюється нічого, тому в
    /// історію не потрапляє нічого: інакше там з'явився б рядок із тією самою
    /// ціною, який лише спантеличив би інших учасників.
    /// </summary>
    private List<Bid> RaiseOwnCeiling(decimal maxAmount, DateTimeOffset now, TimeSpan extension)
    {
        if (maxAmount <= LeaderMaxAmount)
        {
            throw new DomainRuleException("Нова стеля має бути вищою за попередню.");
        }

        LeaderMaxAmount = maxAmount;
        ExtendIfClosing(now, extension);

        return [];
    }

    /// <summary>
    /// Антиснайпінг. Ставка в останню хвилину відсуває фінал ще на хвилину,
    /// і так доки ставки не вщухнуть. Ліміту продовжень немає навмисно:
    /// снайперові досить було б дочекатися, поки ліміт вичерпається.
    /// </summary>
    private void ExtendIfClosing(DateTimeOffset now, TimeSpan extension)
    {
        if (EndsAt - now < extension)
        {
            EndsAt = now.Add(extension);
        }
    }

    private void EnsureRunning(DateTimeOffset now)
    {
        if (Status != AuctionStatus.Active)
        {
            throw new DomainRuleException("Торги вже завершені.");
        }

        if (now >= EndsAt)
        {
            throw new DomainRuleException("Час торгів вичерпано.");
        }
    }

    private Bid NewBid(
        long bidderId,
        decimal amount,
        decimal? maxAmount,
        bool isAutomatic,
        DateTimeOffset now)
    {
        return new Bid
        {
            AuctionId = Id,
            BidderId = bidderId,
            Amount = amount,
            MaxAmount = maxAmount,
            IsAutomatic = isAutomatic,
            CreatedAt = now,
        };
    }

    /// <summary>
    /// Закриває торги й підбиває підсумок. Викликається задачею планувальника,
    /// коли вийшов час.
    ///
    /// Повертає <c>false</c>, якщо торги вже були закриті. Це НЕ помилка:
    /// задача може спрацювати двічі — планувальник перезапустився, інстансів
    /// кілька, — і повторний виклик має просто нічого не робити. Виняток тут
    /// означав би, що звичайний перезапуск сервера сиплеться помилками.
    /// </summary>
    public bool Close(DateTimeOffset now)
    {
        if (Status != AuctionStatus.Active)
        {
            return false;
        }

        if (now < EndsAt)
        {
            throw new DomainRuleException("Торги ще тривають — закривати зарано.");
        }

        Status = AuctionStatus.Ended;

        // Переможець є лише тоді, коли ставки були І ціна дотягнула до
        // резерву. Найвища ставка сама собою угоди не робить: продавець від
        // початку сказав, за скільки згоден віддати (SPEC §4).
        WinnerId = HasBids && IsReserveMet ? LeaderId : null;

        return true;
    }
}
