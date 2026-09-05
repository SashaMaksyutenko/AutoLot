using AutoLot.Application.Analytics;
using AutoLot.Application.Analytics.Dtos;
using AutoLot.Domain.Enums;
using AutoLot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AutoLot.Infrastructure.Analytics;

/// <summary>
/// Ринкові ціни по моделі.
///
/// Головна складність тут не в математиці, а в тому, що вибірка буває
/// крихітною. На рідкісне авто в країні може висіти три оголошення, і
/// «середня ціна» по них — майже вигадка. Тому дві запобіжні речі:
/// драбинка розширення вибірки й розмір вибірки, який повертається завжди.
/// </summary>
internal sealed class PriceAnalyticsService(AutoLotDbContext dbContext) : IPriceAnalyticsService
{
    /// <summary>
    /// Менше за це — не показуємо нічого.
    /// </summary>
    /// <remarks>
    /// Три, а не двадцять: на рідкісні моделі двадцяти оголошень не буває
    /// ніколи, і високий поріг просто вимкнув би аналітику там, де вона
    /// найпотрібніша. Захист від хибного враження дає не поріг, а те, що
    /// кількість завжди на видноті поруч із цифрою.
    /// </remarks>
    private const int MinimumSample = 3;

    public async Task<PriceStats?> ForModelAsync(
        long modelId,
        int? year,
        CancellationToken cancellationToken = default)
    {
        // Драбинка: спершу найточніше — та сама модель того самого року.
        // Якщо таких мало, розширюємо до всієї моделі. Далі не йдемо:
        // «середня по всіх Audi» не каже про A4 нічого.
        if (year is { } wanted)
        {
            var exact = await CollectAsync(modelId, wanted, cancellationToken);

            if (exact is not null)
            {
                return exact;
            }
        }

        return await CollectAsync(modelId, year: null, cancellationToken);
    }

    public async Task<PriceInsight?> ForListingAsync(
        long listingId,
        CancellationToken cancellationToken = default)
    {
        var listing = await dbContext.Listings
            .AsNoTracking()
            .Where(item => item.Id == listingId)
            .Select(item => new
            {
                item.Car.ModelId,
                item.Car.Year,
                item.PriceUah,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (listing is null)
        {
            return null;
        }

        // Тут — ЛИШЕ той самий рік, без розширення до всієї моделі.
        //
        // Драбинка доречна для загальної довідки «скільки коштують Passat»,
        // але не для твердження «це авто дешевше за ринок». Рік — найбільший
        // чинник ціни: Passat 2009 проти медіани, що змішала 2009 і 2020,
        // отримує «−75%», і це вводить в оману сильніше, ніж мовчання.
        var market = await CollectAsync(listing.ModelId, listing.Year, cancellationToken);

        if (market is null)
        {
            return null;
        }

        return new PriceInsight(market, listing.PriceUah, PercentFrom(market.Median, listing.PriceUah));
    }

    /// <summary>
    /// Збирає ціни однієї вибірки. Повертає <c>null</c>, якщо оголошень
    /// менше за поріг.
    /// </summary>
    private async Task<PriceStats?> CollectAsync(
        long modelId,
        int? year,
        CancellationToken cancellationToken)
    {
        var listings = dbContext.Listings
            .AsNoTracking()
            .Where(listing => listing.Status == ListingStatus.Active && listing.Car.ModelId == modelId);

        if (year is { } wanted)
        {
            listings = listings.Where(listing => listing.Car.Year == wanted);
        }

        // Тягнемо самі ціни, одним стовпцем. Медіану в базі порахувати можна,
        // але кожна СУБД робить це по-своєму, а вибірка тут — оголошення
        // однієї моделі, тобто десятки, зрідка сотні чисел.
        var prices = await listings
            .Select(listing => listing.PriceUah)
            .ToListAsync(cancellationToken);

        // Упорядковуємо вже в пам'яті, а не запитом. Так дешевше (сортувати
        // все одно довелося б для медіани) і надійніше: SQLite зберігає
        // decimal текстом і порівнює його розбором за поточною культурою —
        // на машині з комою як роздільником запит просто падає.
        prices.Sort();

        if (prices.Count < MinimumSample)
        {
            return null;
        }

        var names = await dbContext.Models
            .AsNoTracking()
            .Where(model => model.Id == modelId)
            .Select(model => new { Make = model.Make.Name, Model = model.Name })
            .FirstOrDefaultAsync(cancellationToken);

        return new PriceStats(
            prices.Count,
            year is null ? PriceBasis.Model : PriceBasis.ModelAndYear,
            names?.Make ?? string.Empty,
            names?.Model ?? string.Empty,
            year,
            Median(prices),
            Math.Round(prices.Average(), 0),
            prices[0],
            prices[^1]);
    }

    /// <summary>
    /// Медіана вже впорядкованого списку. Для парної кількості — середнє двох
    /// середніх: інакше «медіана» стрибала б залежно від того, який із двох
    /// сусідів узяти.
    /// </summary>
    private static decimal Median(List<decimal> sorted)
    {
        var middle = sorted.Count / 2;

        return sorted.Count % 2 == 1
            ? sorted[middle]
            : Math.Round((sorted[middle - 1] + sorted[middle]) / 2m, 0);
    }

    /// <summary>
    /// На скільки відсотків ціна відрізняється від медіани. Від'ємне —
    /// дешевше за ринок.
    /// </summary>
    private static int PercentFrom(decimal median, decimal price)
    {
        if (median <= 0)
        {
            return 0;
        }

        return (int)Math.Round((price - median) / median * 100m, MidpointRounding.AwayFromZero);
    }
}
