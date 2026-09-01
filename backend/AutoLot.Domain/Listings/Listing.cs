using AutoLot.Domain.Common;
using AutoLot.Domain.Dealers;
using AutoLot.Domain.Enums;
using AutoLot.Domain.Geo;
using AutoLot.Domain.Identity;

namespace AutoLot.Domain.Listings;

/// <summary>
/// Оголошення про продаж. Саме воно проходить модерацію, має ціну й статус;
/// технічні характеристики винесені в <see cref="Car"/>.
/// </summary>
public sealed class Listing : AuditableEntity
{
    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Хто фізично подав оголошення. Завжди людина — навіть коли лот
    /// належить салону: там його подає конкретний менеджер, і слід про це
    /// має лишатися.
    /// </summary>
    public long SellerId { get; set; }

    public User Seller { get; set; } = null!;

    /// <summary>
    /// Чий це лот, якщо продає салон. Порожній у приватної особи.
    ///
    /// Поле ДОДАНЕ до <see cref="SellerId"/>, а не замість нього. Спокуса
    /// зробити продавцем салон хибна: тоді SellerId перестав би бути людиною,
    /// і кожна перевірка прав у проєкті вимагала б переписування. А так
    /// правило лише розширюється: «моє, якщо я подав АБО я працюю в салоні,
    /// якому воно належить».
    /// </summary>
    public long? DealershipId { get; set; }

    public Dealership? Dealership { get; set; }

    // ── Де продають ──────────────────────────────────────────────────

    public long CityId { get; set; }

    public City City { get; set; } = null!;

    public long? CityDistrictId { get; set; }

    public CityDistrict? CityDistrict { get; set; }

    // ── Ціна ─────────────────────────────────────────────────────────

    public decimal Price { get; set; }

    public Currency Currency { get; set; }

    /// <summary>
    /// Ціна, перерахована в гривню за курсом НБУ (SPEC §7). Потрібна, щоб
    /// сортувати й фільтрувати оголошення в різних валютах разом; сама ціна
    /// при цьому зберігається такою, як її ввів продавець.
    /// </summary>
    public decimal PriceUah { get; set; }

    /// <summary>
    /// Для лота з торгами <see cref="Price"/> — це стартова ціна, а це поле —
    /// нижня межа, за якою продавець згоден віддати авто. Суму не бачить
    /// ніхто: покупцям показують лише бейдж «резерв не досягнуто» (SPEC §4).
    ///
    /// null означає лот без резерву — і це перевага, яку показуємо окремо:
    /// учасник знає, що торгується не намарно.
    /// </summary>
    public decimal? ReservePrice { get; set; }

    // ── Умови угоди ──────────────────────────────────────────────────

    public bool IsNegotiable { get; set; }

    public bool AcceptsTrade { get; set; }

    public bool IsUrgent { get; set; }

    // ── Стан оголошення ──────────────────────────────────────────────

    public ListingType Type { get; set; } = ListingType.FixedPrice;

    public ListingStatus Status { get; set; } = ListingStatus.Draft;

    /// <summary>Коли оголошення вперше стало видимим. Порожнє в чернетки.</summary>
    public DateTimeOffset? PublishedAt { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary>Причина відмови модератора — автор має розуміти, що виправляти.</summary>
    public string? RejectionReason { get; set; }

    /// <summary>Скільки разів картку відкривали. Оновлюється окремо від решти.</summary>
    public int ViewCount { get; set; }

    public Car Car { get; set; } = null!;

    // ── Життєвий цикл ────────────────────────────────────────────────
    //
    // Переходи між статусами живуть тут, а не в сервісі: правило «продане
    // оголошення не можна подати на модерацію» стосується самого оголошення,
    // і якщо його записати збоку, наступний сервіс про нього просто не знатиме.

    /// <summary>Чи можна редагувати. Опубліковане правиться лише через повторну модерацію.</summary>
    public bool IsEditable => Status is ListingStatus.Draft or ListingStatus.Rejected;

    /// <summary>Чи займає місце в ліміті активних оголошень.</summary>
    public bool CountsTowardsLimit =>
        Status is ListingStatus.Active or ListingStatus.PendingModeration;

    /// <summary>
    /// Чи бачить оголошення сторонній. Продане лишається на видноті навмисно:
    /// за ним ходять із закладок і посилань, а ціна проданого — найкорисніше,
    /// що є на майданчику.
    /// </summary>
    public bool IsPublic => Status is ListingStatus.Active or ListingStatus.Sold;

    /// <summary>Автор подає чернетку або виправлене оголошення на розгляд.</summary>
    public void SubmitForModeration()
    {
        if (!IsEditable)
        {
            throw new DomainRuleException(
                "На модерацію можна подати лише чернетку або відхилене оголошення.");
        }

        Status = ListingStatus.PendingModeration;

        // Стару причину відмови прибираємо: вона стосувалася попередньої версії.
        RejectionReason = null;
    }

    /// <summary>Модератор схвалює: оголошення стає видимим і починає жити.</summary>
    public void Approve(DateTimeOffset now, TimeSpan lifetime)
    {
        if (Status is not ListingStatus.PendingModeration)
        {
            throw new DomainRuleException("Схвалити можна лише оголошення, подане на модерацію.");
        }

        Status = ListingStatus.Active;
        RejectionReason = null;

        // Дату першої публікації не перезаписуємо — оголошення могло вже
        // проходити цикл «відхилено → виправлено → схвалено».
        PublishedAt ??= now;
        ExpiresAt = now.Add(lifetime);
    }

    public void Reject(string reason)
    {
        if (Status is not ListingStatus.PendingModeration)
        {
            throw new DomainRuleException("Відхилити можна лише оголошення, подане на модерацію.");
        }

        Status = ListingStatus.Rejected;
        RejectionReason = reason;
    }

    /// <summary>
    /// Модератор знімає з публікації оголошення, на яке надійшла слушна скарга.
    /// </summary>
    /// <remarks>
    /// Чому окремий метод, а не <see cref="Reject"/>: той працює лише з черги
    /// модерації, і не випадково — «відхилити» означає «не пропустити», а тут
    /// оголошення вже пропустили й люди його бачили.
    ///
    /// Стан обираємо той самий, <see cref="ListingStatus.Rejected"/>, і це
    /// свідомо: він єдиний, у якому автор може виправити оголошення й подати
    /// знову. Знята з публікації неправда про пробіг має вести саме до цього,
    /// а не до архіву, звідки виходять лише через нову чернетку.
    /// </remarks>
    public void TakeDown(string reason)
    {
        if (!IsPublic)
        {
            throw new DomainRuleException(
                "Зняти з публікації можна лише оголошення, яке в ній є.");
        }

        Status = ListingStatus.Rejected;
        RejectionReason = reason;
    }

    /// <summary>Автор позначає авто проданим.</summary>
    public void MarkSold()
    {
        if (Status is not ListingStatus.Active)
        {
            throw new DomainRuleException("Проданим можна позначити лише активне оголошення.");
        }

        Status = ListingStatus.Sold;
    }

    /// <summary>Автор прибирає оголошення з видачі, не видаляючи його.</summary>
    public void Archive()
    {
        if (Status is ListingStatus.Archived)
        {
            throw new DomainRuleException("Оголошення вже в архіві.");
        }

        if (Status is ListingStatus.Draft)
        {
            throw new DomainRuleException("Чернетку не архівують — її видаляють.");
        }

        Status = ListingStatus.Archived;
    }

    /// <summary>Повертає архівне оголошення в роботу — знову через модерацію.</summary>
    public void Restore()
    {
        if (Status is not ListingStatus.Archived)
        {
            throw new DomainRuleException("Відновити можна лише архівне оголошення.");
        }

        Status = ListingStatus.Draft;
    }
}
