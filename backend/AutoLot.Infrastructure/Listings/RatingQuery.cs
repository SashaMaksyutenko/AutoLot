using AutoLot.Application.Listings.Dtos;
using AutoLot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AutoLot.Infrastructure.Listings;

/// <summary>
/// Рейтинг людини за відгуками про неї.
///
/// Винесено тієї ж миті, коли з'явилася третя копія: рейтинг потрібен
/// сервісу відгуків, картці авто й публічному профілю. Розійтися їм не
/// можна — одна людина мусить мати однакову оцінку скрізь, де її показують.
/// </summary>
internal static class RatingQuery
{
    public static async Task<RatingSummary> OfAsync(
        AutoLotDbContext dbContext,
        long userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        // Рахуємо в базі, а не в пам'яті: тягнути всі відгуки заради
        // середнього — марна робота, яка зростає разом із репутацією.
        var reviews = dbContext.Reviews
            .AsNoTracking()
            .Where(review => review.SubjectId == userId);

        var count = await reviews.CountAsync(cancellationToken);

        if (count == 0)
        {
            // Нуль відгуків — це не нуль зірок. Клієнт має показати «без
            // відгуків», а не «0,0». Заразом ця перевірка рятує AverageAsync:
            // на порожньому наборі він кидає виняток.
            return new RatingSummary(0, 0m);
        }

        var average = await reviews.AverageAsync(review => (decimal)review.Rating, cancellationToken);

        // Два запити замість одного зведення через GroupBy — і це свідомо.
        // Той трюк давав один похід у базу, але EF справедливо попереджав на
        // нього: після групування в запиті не лишається ні фільтра, ні
        // впорядкування, тож «перший рядок» стає невизначеним. Два простих
        // агрегати по індексованому стовпцю коштують дешево, а читаються
        // прозоро.
        //
        // Один знак після коми: «4,3» читається, «4,333333» — ні.
        return new RatingSummary(
            count,
            Math.Round(average, 1, MidpointRounding.AwayFromZero));
    }
}
