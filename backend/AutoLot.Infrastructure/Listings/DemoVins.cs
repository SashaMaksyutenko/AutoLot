using AutoLot.Domain.Listings;

namespace AutoLot.Infrastructure.Listings;

/// <summary>
/// Складає правдоподібні номери кузова для демонстраційних оголошень.
///
/// Навіщо взагалі. Двісті оголошень без жодного VIN виглядають біднішими, ніж
/// є насправді: номер — одна з ознак довіри, і порожнє поле на кожній картці
/// це помітно. Але важливіше інше: демо-дані мають проходити ВЛАСНІ перевірки
/// проєкту, а не оминати їх. Номер, зібраний абияк, не пройшов би ні звірку
/// року, ні контрольну цифру — тобто показував би сайт таким, яким він не є.
///
/// Тому номер збирається за будовою стандарту:
///
///   позиції 1–3   WMI — виробник, береться з demo-wmi.json за маркою
///   позиції 4–8   опис моделі; для вигаданих даних просто випадкові символи
///   позиція  9    контрольна цифра — дораховується там, де вона обов'язкова
///   позиція  10   модельний рік, узгоджений із роком авто
///   позиція  11   завод; лишаємо сталим
///   позиції 12–17 серійний номер, він і робить кожен VIN унікальним
/// </summary>
internal static class DemoVins
{
    /// <summary>
    /// Символи, з яких складають випадкові частини. Ті самі, що дозволені у
    /// VIN: без I, O та Q, які плутають з одиницею та нулем.
    /// </summary>
    private const string Alphabet = "ABCDEFGHJKLMNPRSTUVWXYZ0123456789";

    /// <summary>Умовний код заводу. Один на всі демо-дані — розрізняти їх нема потреби.</summary>
    private const char Plant = 'W';

    /// <summary>
    /// Збирає номер для авто вказаної марки та року.
    /// </summary>
    /// <returns>
    /// null, якщо марки немає в довіднику префіксів або рік надто давній для
    /// сімнадцятизначного VIN. Оголошення тоді лишається без номера — поле
    /// необов'язкове, і в житті його заповнює теж не кожен.
    /// </returns>
    public static string? Create(
        IReadOnlyDictionary<string, string> prefixes,
        string makeSlug,
        int year,
        Random random)
    {
        ArgumentNullException.ThrowIfNull(prefixes);
        ArgumentNullException.ThrowIfNull(random);

        if (!prefixes.TryGetValue(makeSlug, out var wmi)
            || Vin.YearCode(year) is not { } yearCode)
        {
            return null;
        }

        /*
            Дев'яту позицію поки заповнюємо нулем-заглушкою. Її вага в
            підрахунку нульова, тож на результат вона не впливає, — а потім ми
            просто ставимо на її місце те, що вийшло.
        */
        var vin = string.Concat(
            wmi,
            RandomPart(random, 5),
            "0",
            yearCode,
            Plant,
            RandomDigits(random, 6));

        /*
            Контрольну цифру дораховуємо лише там, де вона обов'язкова за
            стандартом, — у номерах з Північної Америки. Ставити правильну
            цифру європейському номеру шкоди не буде, але й сенсу теж: там її
            ніхто не перевіряє, а так демо-дані точніше схожі на справжні.
        */
        if (!Vin.RequiresCheckDigit(vin))
        {
            return vin;
        }

        var checkDigit = Vin.CheckDigitFor(vin);

        return checkDigit is null
            ? null
            : string.Concat(vin.AsSpan(0, 8), checkDigit.Value.ToString(), vin.AsSpan(9));
    }

    private static string RandomPart(Random random, int length) =>
        string.Create(length, random, (span, source) =>
        {
            for (var index = 0; index < span.Length; index++)
            {
                span[index] = Alphabet[source.Next(Alphabet.Length)];
            }
        });

    /// <summary>
    /// Серійна частина — самі цифри. Так роблять більшість виробників, і так
    /// номер легше прочитати очима на картці авто.
    /// </summary>
    private static string RandomDigits(Random random, int length) =>
        string.Create(length, random, (span, source) =>
        {
            for (var index = 0; index < span.Length; index++)
            {
                span[index] = (char)('0' + source.Next(10));
            }
        });
}
