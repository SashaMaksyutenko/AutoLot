namespace AutoLot.Domain.Listings;

/// <summary>
/// Розбір VIN — сімнадцятизначного номера кузова.
///
/// VIN не випадковий рядок: у нього є будова, задана стандартом ISO 3779, і з
/// нього можна дістати дані, нічого нікуди не запитуючи. Досі проєкт перевіряв
/// лише довжину й набір літер, тобто приймав будь-яку вигадку з сімнадцяти
/// символів.
///
/// Тут — дві перевірки, обидві чиста арифметика:
///
/// • контрольна цифра (дев'ята позиція) рахується з решти шістнадцяти за
///   фіксованим правилом і ловить помилку в одному символі;
/// • рік випуску (десята позиція) можна звірити з тим, що ввів продавець.
///
/// Літер I, O та Q у VIN немає навмисно: їх надто легко сплутати з одиницею
/// та нулем.
/// </summary>
public static class Vin
{
    public const int Length = 17;

    /// <summary>Позиції рахуємо від одиниці, як у стандарті; у масиві це індекс на одиницю менший.</summary>
    private const int CheckDigitPosition = 9;

    private const int YearPosition = 10;

    /// <summary>Перший рік, з якого VIN узагалі має сімнадцять символів.</summary>
    public const int FirstStandardYear = 1981;

    /// <summary>
    /// Літери в порядку, у якому вони позначають модельний рік, а далі цифри.
    /// Разом тридцять значень — саме такий цикл, тож той самий символ означає
    /// роки, що відстоять на тридцять: «M» це і 1991, і 2021.
    ///
    /// I, O, Q пропущені як скрізь у VIN; U та Z — додатково саме в цій
    /// позиції, а нуль не використовується взагалі.
    /// </summary>
    private const string YearCodes = "ABCDEFGHJKLMNPRSTVWXY123456789";

    /// <summary>Рік, який позначає перший символ таблиці вище.</summary>
    private const int FirstCodedYear = 1980;

    private const int YearCycle = 30;

    /// <summary>
    /// Ваги позицій для контрольної цифри. Дев'ята — нуль, бо сама контрольна
    /// цифра в підрахунок себе не входить.
    /// </summary>
    private static readonly int[] Weights = [8, 7, 6, 5, 4, 3, 2, 10, 0, 9, 8, 7, 6, 5, 4, 3, 2];

    /// <summary>
    /// Числове значення літери. Цифри означають самі себе, а літери зводяться
    /// до одиниць-дев'яток за таблицею стандарту — вона не алфавітна, тож
    /// вивести її формулою не вийде, лише виписати.
    /// </summary>
    private static readonly Dictionary<char, int> LetterValues = new()
    {
        ['A'] = 1, ['B'] = 2, ['C'] = 3, ['D'] = 4, ['E'] = 5, ['F'] = 6, ['G'] = 7, ['H'] = 8,
        ['J'] = 1, ['K'] = 2, ['L'] = 3, ['M'] = 4, ['N'] = 5, ['P'] = 7, ['R'] = 9,
        ['S'] = 2, ['T'] = 3, ['U'] = 4, ['V'] = 5, ['W'] = 6, ['X'] = 7, ['Y'] = 8, ['Z'] = 9,
    };

    /// <summary>
    /// Зводить написаний як завгодно VIN до одного вигляду: без пробілів, самі
    /// великі літери. Порожній рядок стає null — «не вказано».
    ///
    /// Потрібно двом місцям одразу: пошуку дублікатів (інакше «wvw…» і «WVW…»
    /// були б різними номерами) і перевіркам нижче.
    /// </summary>
    public static string? Normalize(string? value)
    {
        var trimmed = value?.Trim();

        return string.IsNullOrEmpty(trimmed)
            ? null
            : trimmed.ToUpperInvariant();
    }

    /// <summary>
    /// Чи схоже це взагалі на VIN: сімнадцять символів, лише цифри й дозволені
    /// літери. Це найгрубіша перевірка — саме її проєкт робив досі.
    /// </summary>
    public static bool HasValidShape(string? vin)
    {
        if (vin is not { Length: Length })
        {
            return false;
        }

        foreach (var symbol in vin)
        {
            var allowed = char.IsAsciiDigit(symbol)
                || (char.IsAsciiLetterUpper(symbol) && symbol is not ('I' or 'O' or 'Q'));

            if (!allowed)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Чи зобов'язаний цей VIN мати правильну контрольну цифру.
    ///
    /// Головне застереження всього класу. Контрольна цифра обов'язкова в
    /// Північній Америці, а в Європі та Азії — ні: там дев'яту позицію часто
    /// заповнюють чим завгодно. Вимагати її від усіх означало б відхиляти
    /// цілком справжні номери європейських авто, яких на нашому ринку
    /// більшість.
    ///
    /// Регіон видно з першого символу: 1, 4, 5 — США, 2 — Канада, 3 — Мексика.
    /// </summary>
    public static bool RequiresCheckDigit(string? vin) =>
        HasValidShape(vin) && vin![0] is >= '1' and <= '5';

    /// <summary>
    /// Рахує контрольну цифру з решти символів і звіряє з тією, що стоїть
    /// дев'ятою. Помилка в одному символі майже завжди змінює результат —
    /// саме для цього така цифра й потрібна.
    /// </summary>
    public static bool IsCheckDigitValid(string? vin)
    {
        if (!HasValidShape(vin))
        {
            return false;
        }

        var sum = 0;

        for (var index = 0; index < Length; index++)
        {
            if (!ValueOf(vin![index], out var value))
            {
                return false;
            }

            sum += value * Weights[index];
        }

        // Залишок 10 позначають літерою X — окремої цифри для десяти немає.
        var expected = sum % 11;
        var actual = vin![CheckDigitPosition - 1];

        return expected == 10
            ? actual == 'X'
            : actual == (char)('0' + expected);
    }

    /// <summary>
    /// Роки, які може означати десята позиція.
    /// </summary>
    /// <remarks>
    /// Їх завжди кілька: цикл позначень триває тридцять років і повторюється.
    /// Який саме з них — з самого VIN не видно, тому повертаємо всі можливі, а
    /// звіряти з роком від продавця — справа того, хто викликає.
    /// </remarks>
    /// <param name="upTo">Найпізніший рік, який має сенс розглядати.</param>
    public static IReadOnlyList<int> PossibleYears(string? vin, int upTo)
    {
        if (!HasValidShape(vin))
        {
            return [];
        }

        var offset = YearCodes.IndexOf(vin![YearPosition - 1], StringComparison.Ordinal);

        if (offset < 0)
        {
            return [];
        }

        var years = new List<int>();

        for (var year = FirstCodedYear + offset; year <= upTo; year += YearCycle)
        {
            if (year >= FirstStandardYear)
            {
                years.Add(year);
            }
        }

        return years;
    }

    /// <summary>
    /// Чи узгоджується рік, названий продавцем, із тим, що зашито у VIN.
    /// </summary>
    /// <remarks>
    /// Розбіжність на рік вважаємо нормальною навмисно: у VIN стоїть
    /// МОДЕЛЬНИЙ рік, а він починається восени попереднього календарного.
    /// Авто, випущене в листопаді 2020-го, цілком законно має модельний
    /// 2021-й — і продавець, який дивиться в техпаспорт, напише 2020.
    /// </remarks>
    public static bool MatchesYear(string? vin, int year, int upTo)
    {
        var possible = PossibleYears(vin, upTo);

        return possible.Count == 0 || possible.Any(candidate => Math.Abs(candidate - year) <= 1);
    }

    private static bool ValueOf(char symbol, out int value)
    {
        if (char.IsAsciiDigit(symbol))
        {
            value = symbol - '0';
            return true;
        }

        return LetterValues.TryGetValue(symbol, out value);
    }
}
