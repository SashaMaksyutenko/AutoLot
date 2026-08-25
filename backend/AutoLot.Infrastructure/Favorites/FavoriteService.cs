using AutoLot.Application.Common;
using AutoLot.Application.Common.Abstractions;
using AutoLot.Application.Favorites;
using AutoLot.Application.Listings;
using AutoLot.Application.Listings.Dtos;
using AutoLot.Domain.Enums;
using AutoLot.Domain.Listings;
using AutoLot.Infrastructure.Listings;
using AutoLot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AutoLot.Infrastructure.Favorites;

/// <summary>
/// Обране. Позначку можна поставити лише на оголошення, яке видно всім:
/// інакше за кодом відповіді можна було б навпомацки перевіряти, чи існує
/// чужа чернетка (SPEC §8).
/// </summary>
internal sealed class FavoriteService(
    AutoLotDbContext dbContext,
    IDateTimeProvider clock,
    ListingMapper mapper) : IFavoriteService
{
    /// <summary>
    /// Скільки карток віддаємо за раз. Обране гортають рідше за каталог,
    /// тож сторінка більша — менше натискань на «далі».
    /// </summary>
    private const int MaxPageSize = 60;

    /// <summary>
    /// Стани, у яких оголошення видно всім. Продане лишається видимим —
    /// покупцеві корисно побачити, що відкладене авто вже пішло. Чернетки,
    /// відхилені та архівні сюди не входять.
    ///
    /// Саме масив, а не метод: усе, що стоїть усередині запиту до бази, EF
    /// перекладає в SQL, а виклик власного методу перекласти неможливо.
    /// Contains по масиву ж перетворюється на звичайне SQL-ове IN (...).
    /// </summary>
    private static readonly ListingStatus[] PubliclyVisible =
        [ListingStatus.Active, ListingStatus.Sold];

    public async Task<bool> AddAsync(
        long userId,
        long listingId,
        CancellationToken cancellationToken = default)
    {
        var isPublic = await dbContext.Listings
            .AsNoTracking()
            .AnyAsync(
                listing => listing.Id == listingId && PubliclyVisible.Contains(listing.Status),
                cancellationToken);

        if (!isPublic)
        {
            throw new ListingNotFoundException(listingId);
        }

        var alreadyThere = await dbContext.Favorites
            .AsNoTracking()
            .AnyAsync(
                favorite => favorite.UserId == userId && favorite.ListingId == listingId,
                cancellationToken);

        if (alreadyThere)
        {
            return false;
        }

        dbContext.Favorites.Add(new Favorite
        {
            UserId = userId,
            ListingId = listingId,
            CreatedAt = clock.UtcNow,
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> RemoveAsync(
        long userId,
        long listingId,
        CancellationToken cancellationToken = default)
    {
        // ExecuteDeleteAsync видаляє прямо в базі одним запитом, не вантажачи
        // рядок у пам'ять заради того, щоб одразу його викинути. Повертає
        // кількість видалених рядків — нам достатньо знати, чи був хоч один.
        var deleted = await dbContext.Favorites
            .Where(favorite => favorite.UserId == userId && favorite.ListingId == listingId)
            .ExecuteDeleteAsync(cancellationToken);

        return deleted > 0;
    }

    public async Task<PagedResult<ListingSummary>> GetPageAsync(
        long userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var favorites = MyVisibleFavorites(userId);

        var totalCount = await favorites.CountAsync(cancellationToken);

        // Спочатку відбираємо потрібну сторінку позначок і лише потім
        // переходимо до оголошень — так порядок лишається «за часом
        // додавання в обране», а не за датою публікації.
        var listings = favorites
            .OrderByDescending(favorite => favorite.CreatedAt)
            .ThenByDescending(favorite => favorite.ListingId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(favorite => favorite.Listing);

        var items = await mapper.ToSummariesAsync(listings, cancellationToken);

        return new PagedResult<ListingSummary>(items, page, pageSize, totalCount);
    }

    public async Task<int> CountAsync(long userId, CancellationToken cancellationToken = default)
    {
        return await MyVisibleFavorites(userId).CountAsync(cancellationToken);
    }

    /// <summary>
    /// Обране цього користувача, з якого прибрано оголошення, що більше не
    /// показуються всім. Сам рядок у базі лишається: якщо автор поверне
    /// оголошення з архіву, позначка знову спрацює.
    /// </summary>
    private IQueryable<Favorite> MyVisibleFavorites(long userId)
    {
        return dbContext.Favorites
            .AsNoTracking()
            .Where(favorite =>
                favorite.UserId == userId && PubliclyVisible.Contains(favorite.Listing.Status));
    }
}
