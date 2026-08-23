using AutoLot.Domain.Enums;

namespace AutoLot.Application.Common.Abstractions;

/// <summary>
/// Курс валюти до гривні. Потрібен, щоб зберігати нормалізовану ціну
/// (SPEC §7) і порівнювати оголошення в різних валютах між собою.
///
/// Поки що джерело — конфігурація; за планом його замінить щоденна задача,
/// яка тягне курс з API НБУ. Саме тому це інтерфейс, а не статичний клас.
/// </summary>
public interface IExchangeRateProvider
{
    /// <summary>Скільки гривень коштує одна одиниця валюти.</summary>
    Task<decimal> GetRateToUahAsync(Currency currency, CancellationToken cancellationToken = default);
}
