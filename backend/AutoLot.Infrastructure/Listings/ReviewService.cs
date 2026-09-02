using AutoLot.Application.Common.Abstractions;
using AutoLot.Application.Listings;
using AutoLot.Application.Listings.Dtos;
using AutoLot.Domain.Enums;
using AutoLot.Domain.Listings;
using AutoLot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AutoLot.Infrastructure.Listings;

/// <summary>
/// Відгуки про угоду.
///
/// Хто про кого пише, тут не питають, а виводять: у проданого лота є рівно
/// дві сторони — той, хто ним керує, і той, кого записали покупцем. Якщо
/// пише один, відгук про другого. Через це неможливо приписати відгук
/// сторонньому, навіть підробивши запит: у тілі просто немає поля «про кого».
/// </summary>
internal sealed class ReviewService(
    AutoLotDbContext dbContext,
    IDateTimeProvider clock,
    ListingAccess access) : IReviewService
{
    public async Task<DealReviews> GetForListingAsync(
        long listingId,
        long? viewerId,
        CancellationToken cancellationToken = default)
    {
        var listing = await dbContext.Listings
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == listingId, cancellationToken)
            ?? throw new ListingNotFoundException(listingId);

        var reviews = await Project(Reviews().Where(review => review.ListingId == listingId))
            .ToListAsync(cancellationToken);

        if (viewerId is not { } viewer)
        {
            // Гість бачить усе написане — заради нього відгуки й публічні.
            // Не бачить лише «свого»: у нього його немає.
            return new DealReviews(CanReview: false, reviews, MineId: null);
        }

        var side = await SideOfAsync(listing, viewer, cancellationToken);

        var mine = reviews.FirstOrDefault(review => review.AuthorId == viewer);

        // Написати можна, якщо ти сторона угоди й ще не писав. Повторно —
        // ні: відгук незмінний, інакше він перестав би бути свідченням.
        var canReview = side is not null && mine is null;

        return new DealReviews(canReview, reviews, mine?.Id);
    }

    public async Task<ReviewRecord> LeaveAsync(
        long listingId,
        long authorId,
        LeaveReviewRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var listing = await dbContext.Listings
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == listingId, cancellationToken)
            ?? throw new ListingNotFoundException(listingId);

        var side = await SideOfAsync(listing, authorId, cancellationToken)
            ?? throw new ReviewNotAllowedException(
                "Відгук лишають сторони угоди — продавець і покупець.");

        var alreadyWritten = await dbContext.Reviews
            .AsNoTracking()
            .AnyAsync(
                review => review.ListingId == listingId && review.AuthorId == authorId,
                cancellationToken);

        if (alreadyWritten)
        {
            throw new ReviewNotAllowedException("Ви вже лишили відгук про цю угоду.");
        }

        var review = Review.Create(
            listingId,
            authorId,
            side.SubjectId,
            side.IsSeller,
            request.Rating,
            request.Text,
            clock.UtcNow);

        dbContext.Reviews.Add(review);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await Project(Reviews().Where(item => item.Id == review.Id))
            .FirstAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ReviewRecord>> GetAboutAsync(
        long userId,
        CancellationToken cancellationToken = default)
    {
        return await Project(
                Reviews()
                    .Where(review => review.SubjectId == userId)
                    .OrderByDescending(review => review.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public Task<RatingSummary> GetRatingAsync(
        long userId,
        CancellationToken cancellationToken = default) =>
        RatingQuery.OfAsync(dbContext, userId, cancellationToken);

    /// <summary>
    /// З якого боку угоди ця людина — і, отже, про кого писатиме. <c>null</c>
    /// означає «ні з якого»: сторонній, або угоди ще не було.
    /// </summary>
    private async Task<(long SubjectId, bool IsSeller)?> SideOfAsync(
        Listing listing,
        long userId,
        CancellationToken cancellationToken)
    {
        // Немає угоди — немає про що писати. Продане «поза майданчиком»
        // теж сюди: покупця не записано, отже другої сторони не існує.
        if (listing.Status != ListingStatus.Sold || listing.BuyerId is not { } buyerId)
        {
            return null;
        }

        if (userId == buyerId)
        {
            // Покупець пише про того, хто подав оголошення. Для салонного
            // лота це менеджер, а не салон: оцінюють того, з ким мали справу,
            // а репутація салону складеться з оцінок його людей.
            return (listing.SellerId, false);
        }

        if (await access.CanManageAsync(listing, userId, cancellationToken))
        {
            return (buyerId, true);
        }

        return null;
    }

    private IQueryable<Review> Reviews() => dbContext.Reviews.AsNoTracking();

    /// <summary>
    /// Спільна проєкція. Винесена, бо однакова в трьох місцях, а розійтися
    /// їм не можна: відгук має виглядати однаково скрізь, де його показують.
    /// </summary>
    /// <remarks>
    /// Приймає вже відфільтрований запит, а не фільтрує сам, і це не примха.
    /// Усе, що йде ПІСЛЯ Select у DTO, EF перекласти в SQL не може: там уже
    /// не таблиця, а виклик конструктора. Тому спершу Where і OrderBy —
    /// і лише потім проєкція.
    /// </remarks>
    private static IQueryable<ReviewRecord> Project(IQueryable<Review> source)
    {
        return source
            .Select(review => new ReviewRecord(
                review.Id,
                review.ListingId,
                review.Listing.Title,
                review.AuthorId,
                review.Author.DisplayName,
                review.SubjectId,
                review.Rating,
                review.Text,
                review.CreatedAt,
                review.AuthorIsSeller));
    }
}
