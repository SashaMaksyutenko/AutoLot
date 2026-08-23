using AutoLot.Application.Common.Abstractions;
using AutoLot.Domain.Enums;
using Microsoft.Extensions.Options;

namespace AutoLot.Infrastructure.Listings;

internal sealed class ConfiguredExchangeRateProvider(IOptions<ExchangeRateOptions> options)
    : IExchangeRateProvider
{
    public Task<decimal> GetRateToUahAsync(
        Currency currency,
        CancellationToken cancellationToken = default)
    {
        if (!options.Value.ToUah.TryGetValue(currency.ToString(), out var rate) || rate <= 0)
        {
            // Мовчазний нуль перетворив би всі ціни на нуль і зіпсував
            // сортування, тож краще впасти одразу.
            throw new InvalidOperationException(
                $"Курс для валюти {currency} не налаштований у секції {ExchangeRateOptions.SectionName}.");
        }

        return Task.FromResult(rate);
    }
}
