using AutoLot.Domain.Enums;
using AutoLot.Domain.Listings;

namespace AutoLot.Infrastructure.Listings;

/// <summary>
/// Вигадує історію ціни для демонстраційного оголошення.
///
/// Без неї графік на картці авто не показався б жодного разу: історія
/// наповнюється, лише коли продавець міняє ціну, а вигадані оголошення ніхто
/// не міняє. Тобто зроблена можливість лишалася б невидимою — рівно та сама
/// біда, що була з порожніми аукціонами й відсутніми VIN.
///
/// Історія будується НАЗАД від нинішньої ціни: та, що в оголошенні зараз, —
/// остання точка, а попередні були вищими. Так воно й буває: авто виставляють
/// дорожче, а потім поступово знижують, доки не знайдеться покупець.
/// </summary>
internal static class DemoPrices
{
    /// <summary>Кожне яке оголошення отримує історію. Решта стоїть за початковою ціною.</summary>
    private const int WithHistoryEvery = 3;

    private const int MaxChanges = 3;

    /// <summary>
    /// На скільки відсотків ціна була вищою на попередньому кроці.
    /// </summary>
    /// <remarks>
    /// Межі невипадкові. Менше за 3% — і графік виглядав би рівною лінією, бо
    /// такий рух не видно оком. Більше за 12% за один крок — і це вже не
    /// «поступове зниження», а помилка в ціні, яку продавець виправив.
    /// </remarks>
    private const int MinStepPercent = 3;
    private const int MaxStepPercent = 12;

    /// <summary>
    /// Збирає точки історії для оголошення — від найдавнішої до нинішньої.
    /// </summary>
    /// <param name="publishedAt">Від цього моменту й рахуємо: раніше оголошення не існувало.</param>
    /// <returns>Порожній перелік, якщо цьому оголошенню історія не дісталася.</returns>
    public static List<PriceChange> Create(
        Listing listing,
        Random random,
        DateTimeOffset publishedAt,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(listing);
        ArgumentNullException.ThrowIfNull(random);

        // У торгах ціну ведуть ставки, а не продавець: історії змін там не буває.
        if (listing.Type is ListingType.Auction)
        {
            return [];
        }

        var changes = random.Next(WithHistoryEvery) == 0 ? random.Next(1, MaxChanges + 1) : 0;

        if (changes == 0)
        {
            // Навіть без змін потрібна одна точка — та, з якої все почалося.
            return [Point(listing, listing.Price, publishedAt)];
        }

        /*
            Йдемо назад: від нинішньої ціни піднімаємося до тієї, з якої
            оголошення починалося. Тому список наприкінці розвертаємо —
            історія має читатися від давнішого до свіжішого.
        */
        var points = new List<PriceChange>(changes + 1);
        var price = listing.Price;
        var moment = now;

        var window = now - publishedAt;

        for (var step = 0; step <= changes; step++)
        {
            points.Add(Point(listing, decimal.Round(price, 2), moment));

            var older = MinStepPercent + random.Next(MaxStepPercent - MinStepPercent + 1);

            price *= 1 + (older / 100m);

            // Кроки назад у часі рівномірні: точна дата тут нічого не важить,
            // а рівні проміжки дають охайний графік.
            moment -= window / (changes + 1);
        }

        points.Reverse();

        // Найдавнішу точку прив'язуємо саме до публікації: історія, що
        // починається пізніше за саме оголошення, виглядала б дивно.
        points[0].ChangedAt = publishedAt;

        return points;
    }

    private static PriceChange Point(Listing listing, decimal price, DateTimeOffset moment) =>
        new()
        {
            Listing = listing,
            Price = price,
            Currency = listing.Currency,

            // Курс беремо той самий, за яким порахована нинішня ціна: для
            // вигаданої історії точність до копійки значення не має.
            PriceUah = listing.Price == 0
                ? price
                : decimal.Round(price * (listing.PriceUah / listing.Price), 2),
            ChangedAt = moment,
        };
}
