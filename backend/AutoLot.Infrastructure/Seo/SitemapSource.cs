using AutoLot.Application.Seo;
using AutoLot.Domain.Enums;
using AutoLot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AutoLot.Infrastructure.Seo;

internal sealed class SitemapSource(AutoLotDbContext dbContext) : ISitemapSource
{
    /// <summary>
    /// Скільки оголошень класти в карту.
    ///
    /// Стандарт дозволяє 50 тисяч адрес в одному файлі. Доки оголошень менше,
    /// ділити карту на частини немає сенсу; коли перевалить — тут і з'явиться
    /// індекс карт, а не мовчазно обрізаний список.
    /// </summary>
    private const int MaxListings = 50_000;

    public async Task<IReadOnlyList<SitemapEntry>> GetAsync(
        CancellationToken cancellationToken = default)
    {
        var entries = new List<SitemapEntry>
        {
            // Каталог — головна сторінка й головне, що варто обходити.
            new("/", null, 1.0),
            new("/dealers", null, 0.5),
        };

        var listings = await dbContext.Listings
            .AsNoTracking()
            .Where(listing => listing.Status == ListingStatus.Active)
            .OrderByDescending(listing => listing.PublishedAt)
            .Take(MaxListings)
            .Select(listing => new { listing.Id, listing.PublishedAt })
            .ToListAsync(cancellationToken);

        entries.AddRange(listings.Select(listing =>
            new SitemapEntry($"/listing/{listing.Id}", listing.PublishedAt, 0.8)));

        var dealers = await dbContext.Dealerships
            .AsNoTracking()
            .Select(dealership => dealership.Slug)
            .ToListAsync(cancellationToken);

        entries.AddRange(dealers.Select(slug => new SitemapEntry($"/dealers/{slug}", null, 0.6)));

        return entries;
    }
}
