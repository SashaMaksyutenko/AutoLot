using AutoLot.Application.Listings.Dtos;

namespace AutoLot.Application.Listings;

/// <summary>
/// Взаємні відгуки після угоди.
///
/// Право написати дає не бажання, а сама угода: відгук лишають ті двоє, між
/// якими вона відбулася, і лише про неї. Тому тут немає методу «написати
/// відгук про людину» — лише «про цю угоду».
/// </summary>
public interface IReviewService
{
    /// <summary>
    /// Стан відгуків під угодою очима того, хто дивиться. Для стороннього
    /// це просто те, що вже написано: відгуки публічні.
    /// </summary>
    Task<DealReviews> GetForListingAsync(
        long listingId,
        long? viewerId,
        CancellationToken cancellationToken = default);

    Task<ReviewRecord> LeaveAsync(
        long listingId,
        long authorId,
        LeaveReviewRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Усе, що написали про людину, найновіше першим.</summary>
    Task<IReadOnlyList<ReviewRecord>> GetAboutAsync(
        long userId,
        CancellationToken cancellationToken = default);

    /// <summary>Рейтинг людини. Порожня історія — це нуль відгуків, а не нуль зірок.</summary>
    Task<RatingSummary> GetRatingAsync(
        long userId,
        CancellationToken cancellationToken = default);
}

/// <summary>Відгук у цьому випадку залишити не можна.</summary>
public sealed class ReviewNotAllowedException(string message) : Exception(message);
