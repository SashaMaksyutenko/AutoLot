using AutoLot.Domain.Enums;

namespace AutoLot.Application.Listings.Dtos;

/// <summary>
/// Одна точка на графіку ціни: скільки коштувало авто й від якого дня.
/// </summary>
/// <param name="Price">Сума у валюті, в якій оголошення було виставлене тоді.</param>
/// <param name="PriceUah">
/// Та сама сума в гривні. Малювати графік треба саме за нею: продавець міг
/// змінити валюту, і без спільної одиниці лінія стрибала б там, де ціна
/// насправді не рухалася.
/// </param>
public sealed record PriceHistoryPoint(
    decimal Price,
    Currency Currency,
    decimal PriceUah,
    DateTimeOffset ChangedAt);
