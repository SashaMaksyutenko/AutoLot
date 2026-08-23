using System.Collections.ObjectModel;
using AutoLot.Domain.Enums;

namespace AutoLot.Infrastructure.Listings;

/// <summary>
/// Курси валют до гривні. Тимчасове джерело: за планом їх щодня
/// оновлюватиме задача, яка тягне курс з API НБУ, і тоді ці значення
/// стануть лише запасним варіантом на випадок недоступності банку.
/// </summary>
public sealed class ExchangeRateOptions
{
    public const string SectionName = "ExchangeRates";

    /// <summary>Скільки гривень коштує одна одиниця валюти.</summary>
    public Dictionary<string, decimal> ToUah { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        [nameof(Currency.Uah)] = 1m,
        [nameof(Currency.Usd)] = 41.50m,
        [nameof(Currency.Eur)] = 45.00m,
    };
}
