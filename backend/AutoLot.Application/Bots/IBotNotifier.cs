using AutoLot.Application.Listings.Dtos;

namespace AutoLot.Application.Bots;

/// <summary>
/// Сповіщення в месенджери.
///
/// Окремо від пошти навмисно: у листа є адреса й підтвердження, у чату —
/// прив'язка, і збіг цих двох умов випадковий. Людина може мати бота без
/// підтвердженої пошти й навпаки.
///
/// Жоден метод не кидає винятків назовні: недоступний месенджер не має
/// зривати те, заради чого виклик і стався — ставку чи розсилку.
/// </summary>
public interface IBotNotifier
{
    /// <summary>
    /// Нові авто за збереженим пошуком. Повертає, у СКІЛЬКИ чатів дійшло:
    /// той, хто кличе, рахує надіслані сповіщення, і «спробували» замість
    /// «надіслали» зробило б цей підрахунок беззмістовним.
    /// </summary>
    Task<int> NotifyNewMatchesAsync(
        long userId,
        string searchName,
        IReadOnlyList<ListingSummary> found,
        int total,
        CancellationToken cancellationToken = default);

    /// <summary>Ставку перебили.</summary>
    Task NotifyOutbidAsync(
        long userId,
        string listingTitle,
        string price,
        long listingId,
        CancellationToken cancellationToken = default);
}
