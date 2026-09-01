using AutoLot.Application.Common.Abstractions;
using AutoLot.Domain.Common;
using AutoLot.Domain.Enums;

namespace AutoLot.Tests.TestDoubles;

/// <summary>Мова запиту з наперед відомим значенням.</summary>
internal sealed class StubLanguage(string code = LanguageCodes.Default) : ICurrentLanguage
{
    public string Code => code;
}

/// <summary>
/// Той, хто нібито виконує запит. У тестах обраного важливий лише
/// ідентифікатор: саме за ним вирішується, чиє обране показувати.
/// null означає гостя.
/// </summary>
internal sealed class StubCurrentUser(long? id = null) : ICurrentUser
{
    public long? Id => id;

    public string? Email => id is null ? null : $"user{id}@example.com";

    public bool IsAuthenticated => id is not null;

    public bool IsInRole(string role) => false;
}

/// <summary>
/// Курс валюти зі сталим значенням. Тестам угоди він потрібен лише тому, що
/// його вимагає конструктор сервісу: жоден із них ціну не перераховує.
/// </summary>
internal sealed class StubExchangeRates(decimal rate = 42m) : IExchangeRateProvider
{
    public Task<decimal> GetRateToUahAsync(
        Currency currency,
        CancellationToken cancellationToken = default) => Task.FromResult(rate);
}
