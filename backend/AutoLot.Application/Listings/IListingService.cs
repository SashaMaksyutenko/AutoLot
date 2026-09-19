using AutoLot.Application.Listings.Dtos;
using AutoLot.Domain.Enums;

namespace AutoLot.Application.Listings;

/// <summary>
/// Робота автора зі своїми оголошеннями. Кожен метод отримує ідентифікатор
/// того, хто діє, і сам перевіряє право на дію — покладатися на контролер
/// у цьому не можна (SPEC §8).
/// </summary>
public interface IListingService
{
    Task<long> CreateAsync(
        long sellerId,
        CreateListingRequest request,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        long listingId,
        long actorId,
        UpdateListingRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Картка оголошення. Чуже неопубліковане оголошення не віддається:
    /// метод поверне <c>null</c>, ніби його не існує.
    /// </summary>
    Task<ListingDetails?> GetAsync(
        long listingId,
        long? actorId,
        bool actorIsModerator,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Оголошення для форми редагування — з ідентифікаторами довідників
    /// замість назв. Повертає <c>null</c>, якщо оголошення немає або воно
    /// чуже: право на редагування перевіряє сам сервіс, не контролер.
    /// </summary>
    Task<ListingDraft?> GetForEditAsync(
        long listingId,
        long actorId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ListingSummary>> GetOwnAsync(
        long sellerId,
        ListingStatus? status,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Що людина купила на майданчику. Дзеркало «моїх оголошень» для
    /// другого боку угоди: без цього списку покупець не має де побачити
    /// свою покупку — а отже, і де лишити відгук.
    /// </summary>
    /// <summary>
    /// Кілька оголошень одним запитом — для порівняння пліч-о-пліч.
    /// Неопубліковані й неіснуючі просто не потрапляють у відповідь: у
    /// порівнянні немає кому показувати «такого немає», там або є колонка,
    /// або її немає.
    /// </summary>
    Task<IReadOnlyList<ListingDetails>> GetManyAsync(
        IReadOnlyList<long> listingIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Що людина нещодавно дивилася, найсвіжіше зверху.
    ///
    /// Історію ведемо лише для тих, хто увійшов: гостя ми не впізнаємо між
    /// заходами, а зберігати щось у його браузері — вже інша річ і з іншими
    /// властивостями (на іншому пристрої такої історії не буде).
    /// </summary>
    Task<IReadOnlyList<ListingSummary>> GetRecentlyViewedAsync(
        long userId,
        int take,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Змінює ціну вже опублікованого оголошення.
    /// </summary>
    /// <remarks>
    /// Окремо від звичайного редагування, і це не примха. Опубліковане
    /// оголошення правити не можна взагалі — інакше після модерації в ньому
    /// підмінили б і фото, і опис. А от ціна змінюється весь час: саме
    /// зниження ціни й рухає продаж, і ганяти оголошення через чергу
    /// модерації щоразу було б безглуздо.
    ///
    /// Кожна зміна лягає в історію — її показує картка авто.
    /// </remarks>
    Task ChangePriceAsync(
        long listingId,
        long actorId,
        decimal price,
        Currency currency,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Як мінялася ціна оголошення, від давнішого до свіжішого.
    /// </summary>
    /// <remarks>
    /// Видимість та сама, що й у самого оголошення: історія ціни чужої
    /// чернетки має бути такою ж недоступною, як і сама чернетка. Тому метод
    /// приймає того, хто питає, а не лише номер лота.
    /// </remarks>
    Task<IReadOnlyList<PriceHistoryPoint>> GetPriceHistoryAsync(
        long listingId,
        long? actorId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Авто, схожі на це: тієї самої марки й у близькій ціні, спершу тієї
    /// самої моделі.
    /// </summary>
    /// <remarks>
    /// Порожньо, якщо саме оголошення стороннім не видно: схожі на чужу
    /// чернетку видали б, що це за авто й скільки воно коштує.
    /// </remarks>
    Task<IReadOnlyList<ListingSummary>> GetSimilarAsync(
        long listingId,
        int take,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ListingSummary>> GetPurchasedAsync(
        long buyerId,
        CancellationToken cancellationToken = default);

    Task SubmitForModerationAsync(
        long listingId,
        long actorId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Хто міг купити це авто — ті, хто писав продавцю про нього. Список
    /// потрібен формі «продано»: змушувати згадувати ім'я вручну означало б
    /// отримати помилки в іменах або порожнє поле.
    /// </summary>
    Task<IReadOnlyList<BuyerCandidate>> GetBuyerCandidatesAsync(
        long listingId,
        long actorId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Позначає авто проданим. Покупець не обов'язковий: продати могли й поза
    /// майданчиком.
    /// </summary>
    Task MarkSoldAsync(
        long listingId,
        long actorId,
        long? buyerId,
        CancellationToken cancellationToken = default);

    Task ArchiveAsync(long listingId, long actorId, CancellationToken cancellationToken = default);

    /// <summary>Видалити можна лише чернетку — решта архівується.</summary>
    Task DeleteDraftAsync(long listingId, long actorId, CancellationToken cancellationToken = default);
}
