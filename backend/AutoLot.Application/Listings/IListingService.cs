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

    Task<IReadOnlyList<ListingSummary>> GetOwnAsync(
        long sellerId,
        ListingStatus? status,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Що людина купила на майданчику. Дзеркало «моїх оголошень» для
    /// другого боку угоди: без цього списку покупець не має де побачити
    /// свою покупку — а отже, і де лишити відгук.
    /// </summary>
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
