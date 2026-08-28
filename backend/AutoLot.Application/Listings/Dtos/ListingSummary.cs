using AutoLot.Domain.Enums;

namespace AutoLot.Application.Listings.Dtos;

/// <summary>
/// Рядок списку: усе, що потрібно намалювати картку у видачі, і нічого
/// зайвого — опис і повні характеристики тягнуть лише на сторінці авто.
/// </summary>
public sealed record ListingSummary(
    long Id,
    string Title,
    ListingType Type,
    ListingStatus Status,
    decimal Price,
    Currency Currency,
    decimal PriceUah,
    string Make,
    string Model,
    int Year,
    int? Mileage,
    FuelType FuelType,
    TransmissionType Transmission,
    string CityName,
    string? PrimaryPhotoPath,
    DateTimeOffset? PublishedAt,

    /// <summary>
    /// Чи відклав це оголошення той, хто зараз дивиться. Для гостя завжди
    /// <c>false</c>: обране прив'язане до акаунта, а не до браузера.
    /// </summary>
    bool IsFavorite,

    /// <summary>
    /// Салон, якщо продає він. Порожнє в приватної особи — і саме за цим
    /// видача малює бейдж.
    /// </summary>
    DealerBadge? Dealer);

/// <summary>
/// Мінімум про салон для картки у видачі: як назвати, куди вести й чи
/// показувати позначку перевіреного. Окремий запис, а не три поля поруч:
/// вони або всі є, або всіх немає.
/// </summary>
public sealed record DealerBadge(string Name, string Slug, bool IsVerified);
