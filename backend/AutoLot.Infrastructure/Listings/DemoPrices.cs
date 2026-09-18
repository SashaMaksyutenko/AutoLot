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
        var interval = (now - publishedAt) / (changes + 1);
        var moment = now;

        // Остання точка — нинішня ціна оголошення, її не чіпаємо.
        var points = new List<PriceChange>(changes + 1) { Point(listing, listing.Price, moment) };

        var exact = listing.Price;
        var previous = listing.Price;

        for (var step = 1; step <= changes; step++)
        {
            var older = MinStepPercent + random.Next(MaxStepPercent - MinStepPercent + 1);

            exact *= 1 + (older / 100m);

            /*
                Округлюємо по-людськи — але не нижче за попередню точку плюс
                один крок. Інакше в дешевого авто зниження на 3% (менше за
                п'ятдесят доларів) після округлення злилося б із сусідньою
                ціною, і в історії з'явилася б «зміна», якої не видно.
            */
            var rounded = Math.Max(Round(exact, listing.Currency), previous + StepOf(listing.Currency));

            // Кроки назад у часі рівномірні: точна дата тут нічого не важить,
            // а рівні проміжки дають охайний графік.
            moment -= interval;

            points.Add(Point(listing, rounded, moment));
            previous = rounded;
        }

        points.Reverse();

        // Найдавнішу точку прив'язуємо саме до публікації: історія, що
        // починається пізніше за саме оголошення, виглядала б дивно.
        points[0].ChangedAt = publishedAt;

        return points;
    }

    /// <summary>
    /// Округлює вже записану історію на місці, не вигадуючи її наново.
    /// </summary>
    /// <remarks>
    /// Навмисно НЕ «видалити й згенерувати»: нова генерація взяла б нову
    /// випадковість, і історія з'явилася б в інших авто, ніж була. Хто вже
    /// бачив графік у конкретному оголошенні, не знайшов би його там знову.
    /// Тут лишаються ті самі авто, та сама кількість змін і ті самі дати —
    /// міняються лише суми.
    ///
    /// Йдемо від кінця: остання точка — це нинішня ціна, вона задана. Кожна
    /// раніша має бути вищою хоча б на крок, інакше після округлення дві
    /// сусідні злилися б у «зміну», якої не видно.
    /// </remarks>
    /// <param name="ordered">Точки від давнішої до свіжішої.</param>
    /// <param name="current">Нинішня ціна оголошення, вже округлена.</param>
    /// <param name="rate">Скільки гривень коштує одиниця валюти оголошення.</param>
    public static void RoundInPlace(
        IReadOnlyList<PriceChange> ordered,
        decimal current,
        Currency currency,
        decimal rate)
    {
        ArgumentNullException.ThrowIfNull(ordered);

        if (ordered.Count == 0)
        {
            return;
        }

        var next = current;

        for (var index = ordered.Count - 1; index >= 0; index--)
        {
            var point = ordered[index];

            point.Price = index == ordered.Count - 1
                ? current
                : Math.Max(Round(point.Price, currency), next + StepOf(currency));

            point.PriceUah = decimal.Round(point.Price * rate, 2);

            next = point.Price;
        }
    }

    /// <summary>
    /// Округлює ціну так, як її виставила б людина.
    /// </summary>
    /// <remarks>
    /// Справжні продавці не пишуть «58 015 $» чи «1 267 118 ₴»: ціну ставлять
    /// круглою, до сотні доларів чи тисячі гривень. Точність до одиниць видає
    /// вигадані дані з першого погляду — і саме це було видно в історії цін,
    /// де стара ціна рахувалася множенням нинішньої на відсоток.
    /// </remarks>
    public static decimal Round(decimal price, Currency currency)
    {
        var step = StepOf(currency);

        /*
            AwayFromZero — так округлюють люди: рівно посередині йдемо вгору.
            За замовчуванням .NET округлює «до парного» (банківське правило),
            і 57 850 стало б 57 800, а не 57 900. Для грошей у звітах це має
            сенс — похибки не накопичуються в один бік, — але ціну на авто так
            ніхто не ставить.
        */
        var rounded = Math.Round(price / step, MidpointRounding.AwayFromZero) * step;

        // Дешеве авто не має округлитися до нуля.
        return Math.Max(rounded, step);
    }

    /// <summary>
    /// Крок округлення. У гривні більший, бо й суми в ній у сорок разів більші:
    /// тисяча гривень — приблизно те саме, що двадцять п'ять доларів.
    /// </summary>
    public static decimal StepOf(Currency currency) => currency is Currency.Uah ? 1_000m : 100m;

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
