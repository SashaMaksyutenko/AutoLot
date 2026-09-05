using AutoLot.Application.Analytics.Dtos;

namespace AutoLot.Application.Analytics;

/// <summary>
/// Середня ринкова ціна по моделі та році (SPEC §12).
///
/// Рахуємо по ОГОЛОШЕННЯХ, які зараз висять, тобто по цінах, які просять.
/// Це не те саме, що ціни, за якими продали, — але саме перше й може знати
/// майданчик, і саме воно потрібне покупцеві, який зараз обирає.
/// </summary>
public interface IPriceAnalyticsService
{
    /// <summary>
    /// Ціни по моделі. <c>null</c>, якщо оголошень надто мало, щоб із них
    /// щось виводити.
    /// </summary>
    Task<PriceStats?> ForModelAsync(
        long modelId,
        int? year,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ціна конкретного оголошення на тлі ринку. <c>null</c>, якщо порівняти
    /// нема з чим — оголошення не знайдено або вибірка замала.
    /// </summary>
    Task<PriceInsight?> ForListingAsync(
        long listingId,
        CancellationToken cancellationToken = default);
}
