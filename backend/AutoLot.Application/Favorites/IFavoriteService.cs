using AutoLot.Application.Common;
using AutoLot.Application.Listings.Dtos;

namespace AutoLot.Application.Favorites;

/// <summary>
/// Обране: особистий список оголошень, які користувач відклав для себе.
///
/// Ідентифікатор користувача передається параметром, а не читається зі
/// сховища всередині: сервіс не має вирішувати, чиє це обране — це справа
/// того, хто його викликає, і саме там перевіряється токен (SPEC §8).
/// </summary>
public interface IFavoriteService
{
    /// <summary>
    /// Додає оголошення в обране. Повертає <c>false</c>, якщо воно там уже
    /// було — повторний виклик не помилка, а просто нічого не змінює.
    /// </summary>
    Task<bool> AddAsync(long userId, long listingId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Прибирає оголошення з обраного. Повертає <c>false</c>, якщо його там
    /// не було.
    /// </summary>
    Task<bool> RemoveAsync(long userId, long listingId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Сторінка обраного, найсвіжіше зверху.
    /// </summary>
    Task<PagedResult<ListingSummary>> GetPageAsync(
        long userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>Скільки всього оголошень у обраному — для лічильника в шапці.</summary>
    Task<int> CountAsync(long userId, CancellationToken cancellationToken = default);
}
