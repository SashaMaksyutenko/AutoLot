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
    DateTimeOffset? PublishedAt);
