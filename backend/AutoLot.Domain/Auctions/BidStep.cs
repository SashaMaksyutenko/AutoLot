using AutoLot.Domain.Enums;

namespace AutoLot.Domain.Auctions;

/// <summary>
/// Крок ставки — наскільки щонайменше нова ставка має перевищити поточну
/// ціну (SPEC §4). Крок прогресивний: на дешевому авто 25 доларів відчутні,
/// на дорогому вони перетворили б торги на сотні дрібних підвищень.
///
/// Клас статичний і не має жодних залежностей: це чиста арифметика, тому
/// його можна перевірити тестами без бази, сервісів і часу.
/// </summary>
public static class BidStep
{
    /// <summary>
    /// Шкала для доларів і євро. Пара означає «поки сума МЕНША за поріг —
    /// діє цей крок». Останній рядок із <see cref="decimal.MaxValue"/> ловить
    /// усе, що вище: так у таблиці немає окремої гілки «інакше».
    /// </summary>
    private static readonly (decimal Below, decimal Step)[] HardCurrencyScale =
    [
        (1_000m, 25m),
        (5_000m, 50m),
        (20_000m, 100m),
        (decimal.MaxValue, 250m),
    ];

    /// <summary>Шкала для гривні — ті самі межі, перераховані в гривневі суми.</summary>
    private static readonly (decimal Below, decimal Step)[] HryvniaScale =
    [
        (40_000m, 1_000m),
        (200_000m, 2_000m),
        (800_000m, 5_000m),
        (decimal.MaxValue, 10_000m),
    ];

    /// <summary>
    /// Крок для поточної ціни. Межі включаються в БІЛЬШИЙ крок: рівно на
    /// 1 000 доларах діє вже крок 50, а не 25, — саме так записано в SPEC
    /// («до 1 000» і «1 000 – 5 000»).
    /// </summary>
    public static decimal For(decimal amount, Currency currency)
    {
        var scale = currency == Currency.Uah ? HryvniaScale : HardCurrencyScale;

        foreach (var (below, step) in scale)
        {
            if (amount < below)
            {
                return step;
            }
        }

        // Сюди не потрапити: останній поріг у шкалі — decimal.MaxValue.
        return scale[^1].Step;
    }

    /// <summary>
    /// Найменша сума, яку приймуть як наступну ставку. Поки ставок немає,
    /// це стартова ціна: перший учасник не мусить перебивати сам себе.
    /// </summary>
    public static decimal MinimumNextBid(decimal currentPrice, bool hasBids, Currency currency)
    {
        return hasBids ? currentPrice + For(currentPrice, currency) : currentPrice;
    }
}
