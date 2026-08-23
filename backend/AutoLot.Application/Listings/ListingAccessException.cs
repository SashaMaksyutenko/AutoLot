namespace AutoLot.Application.Listings;

/// <summary>
/// Дію намагається виконати не той, кому вона належить. Окремо від
/// «не знайдено», бо відповіді різні: 403 проти 404.
/// </summary>
public sealed class ListingAccessException(string message) : Exception(message);
