using AutoLot.Domain.Listings;
using AutoLot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AutoLot.Infrastructure.Listings;

/// <summary>
/// Відповідає на одне питання: чи має ця людина право розпоряджатися цим
/// оголошенням.
///
/// Раніше відповідь була очевидна — «якщо це вона його подала». З появою
/// салонів правило розширилося: оголошенням салону керує будь-хто з його
/// персоналу. Ця перевірка робиться у восьми місцях проєкту, тож живе тут
/// одна: розписана вісім разів, вона неминуче розійшлася б.
/// </summary>
internal sealed class ListingAccess(AutoLotDbContext dbContext)
{
    /// <summary>
    /// Чи може ця людина керувати оголошенням: редагувати, знімати з продажу,
    /// відповідати на питання під ним.
    /// </summary>
    public async Task<bool> CanManageAsync(
        Listing listing,
        long actorId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(listing);

        // Найчастіший випадок — приватна особа зі своїм оголошенням. Він не
        // коштує жодного запиту до бази.
        if (listing.SellerId == actorId)
        {
            return true;
        }

        if (listing.DealershipId is not { } dealershipId)
        {
            return false;
        }

        return await IsMemberAsync(dealershipId, actorId, cancellationToken);
    }

    /// <summary>Чи працює людина в цьому салоні.</summary>
    public async Task<bool> IsMemberAsync(
        long dealershipId,
        long userId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.DealershipMembers
            .AsNoTracking()
            .AnyAsync(
                member => member.DealershipId == dealershipId && member.UserId == userId,
                cancellationToken);
    }

    /// <summary>
    /// Номери салонів, де людина працює. Потрібні там, де перевірка йде не
    /// над одним оголошенням, а над запитом: «покажи все, чим я керую».
    /// Перевіряти кожен рядок окремо означало б запит на кожне оголошення.
    /// </summary>
    public async Task<IReadOnlyList<long>> DealershipIdsOfAsync(
        long userId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.DealershipMembers
            .AsNoTracking()
            .Where(member => member.UserId == userId)
            .Select(member => member.DealershipId)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Звужує вибірку до того, чим людина керує: своє плюс усе, що належить
    /// її салонам.
    /// </summary>
    public static IQueryable<Listing> ManagedBy(
        IQueryable<Listing> listings,
        long actorId,
        IReadOnlyList<long> dealershipIds)
    {
        ArgumentNullException.ThrowIfNull(listings);
        ArgumentNullException.ThrowIfNull(dealershipIds);

        if (dealershipIds.Count == 0)
        {
            return listings.Where(listing => listing.SellerId == actorId);
        }

        return listings.Where(listing =>
            listing.SellerId == actorId
            || (listing.DealershipId != null && dealershipIds.Contains(listing.DealershipId.Value)));
    }
}
