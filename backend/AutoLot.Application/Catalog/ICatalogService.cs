using AutoLot.Application.Common;
using AutoLot.Application.Listings.Dtos;

namespace AutoLot.Application.Catalog;

/// <summary>
/// Публічний каталог. Показує лише активні оголошення — чернетки, відхилені
/// й архівні сюди не потрапляють за жодних параметрів пошуку.
/// </summary>
public interface ICatalogService
{
    Task<PagedResult<ListingSummary>> SearchAsync(
        CatalogQuery query,
        CancellationToken cancellationToken = default);
}
