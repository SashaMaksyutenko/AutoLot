using AutoLot.Domain.Listings;

namespace AutoLot.Tests.Listings;

/// <summary>
/// Розбір VIN.
///
/// Перевіряти тут треба прискіпливо: обидві перевірки — контрольна цифра й рік —
/// можуть ВІДХИЛИТИ оголошення, а помилково відхилене оголошення дорожче за
/// пропущений кривий номер. Тому окремо перевіряється й те, що система НЕ
/// чіпає: європейські номери без контрольної цифри, розбіжність на модельний рік.
/// </summary>
public class VinTests
{
    /// <summary>
    /// Honda Accord 1991 року — класичний приклад зі стандарту. Контрольна
    /// цифра в ньому X, і це правильна відповідь для решти шістнадцяти символів.
    /// </summary>
    private const string HondaAccord1991 = "1HGBH41JXMN109186";

    /// <summary>Volkswagen Golf 2003 року, зібраний у Німеччині.</summary>
    private const string VolkswagenGolf2003 = "WVWZZZ1JZ3W386752";

    private const int Today = 2026;

    // ─────────────────────────── Форма ───────────────────────────

    [Theory]
    [InlineData(HondaAccord1991)]
    [InlineData(VolkswagenGolf2003)]
    public void A_real_vin_has_the_right_shape(string vin)
    {
        Assert.True(Vin.HasValidShape(vin));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1HGBH41JXMN10918")]      // шістнадцять символів
    [InlineData("1HGBH41JXMN1091866")]    // вісімнадцять
    [InlineData("1HGBH41JXMN10918I")]     // літера I
    [InlineData("1HGBH41JXMN10918O")]     // літера O
    [InlineData("1HGBH41JXMN10918Q")]     // літера Q
    [InlineData("1HGBH41JXMN10918-")]     // розділовий знак
    public void Anything_else_is_not_a_vin(string? value)
    {
        Assert.False(Vin.HasValidShape(value));
    }

    [Theory]
    [InlineData("  1hgbh41jxmn109186  ", HondaAccord1991)]
    [InlineData("1HGBH41JXMN109186", HondaAccord1991)]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    public void Normalizing_brings_every_spelling_to_one(string? written, string? expected)
    {
        Assert.Equal(expected, Vin.Normalize(written));
    }

    // ─────────────────────────── Контрольна цифра ───────────────────────────

    [Fact]
    public void A_correct_check_digit_passes()
    {
        Assert.True(Vin.IsCheckDigitValid(HondaAccord1991));
    }

    /// <summary>
    /// Саме заради цього випадку контрольна цифра й існує: один символ
    /// набраний неправильно, решта на місці — і номер більше не сходиться.
    /// </summary>
    [Theory]
    [InlineData("1HGBH41J0MN109186")]     // зіпсована сама контрольна цифра
    [InlineData("1HGBH41JXMN109187")]     // остання цифра
    [InlineData("1HGBH41JXMN209186")]     // цифра всередині
    [InlineData("1HGBN41JXMN109186")]     // літера всередині
    public void One_wrong_symbol_breaks_the_check_digit(string vin)
    {
        Assert.False(Vin.IsCheckDigitValid(vin));
    }

    /// <summary>
    /// Найважливіше застереження всього розбору. Контрольна цифра обов'язкова
    /// лише в Північній Америці; вимагати її від європейського авто означало б
    /// відхиляти справжні номери — а таких на нашому ринку більшість.
    /// </summary>
    [Theory]
    [InlineData("1HGBH41JXMN109186", true)]    // США
    [InlineData("2HGBH41JXMN109186", true)]    // Канада
    [InlineData("3HGBH41JXMN109186", true)]    // Мексика
    [InlineData("WVWZZZ1JZ3W386752", false)]   // Німеччина
    [InlineData("JHMCM56557C404453", false)]   // Японія
    [InlineData("ZFA31200003212345", false)]   // Італія
    public void The_check_digit_is_demanded_only_where_the_rule_applies(string vin, bool required)
    {
        Assert.Equal(required, Vin.RequiresCheckDigit(vin));
    }

    // ─────────────────────────── Рік ───────────────────────────

    /// <summary>
    /// Десята позиція називає модельний рік, але цикл позначень триває
    /// тридцять років — тож той самий символ означає кілька років одразу.
    /// </summary>
    [Fact]
    public void The_year_letter_points_at_every_year_of_its_cycle()
    {
        var years = Vin.PossibleYears(HondaAccord1991, upTo: Today);

        Assert.Equal([1991, 2021], years);
    }

    [Fact]
    public void A_digit_in_the_year_position_works_the_same_way()
    {
        var years = Vin.PossibleYears(VolkswagenGolf2003, upTo: Today);

        Assert.Equal([2003], years);
    }

    /// <summary>
    /// Роки до 1981-го не повертаємо: сімнадцятизначного VIN до того просто
    /// не існувало, тож «1961» був би не відповіддю, а шумом.
    /// </summary>
    [Fact]
    public void Years_before_the_standard_are_not_offered()
    {
        Assert.All(
            Vin.PossibleYears(HondaAccord1991, upTo: Today),
            year => Assert.True(year >= Vin.FirstStandardYear));
    }

    [Theory]
    [InlineData(1991, true)]
    [InlineData(2021, true)]
    [InlineData(1990, true)]     // модельний рік починається восени попереднього
    [InlineData(2022, true)]
    [InlineData(2015, false)]
    [InlineData(1985, false)]
    public void The_stated_year_is_weighed_against_the_vin(int stated, bool matches)
    {
        Assert.Equal(matches, Vin.MatchesYear(HondaAccord1991, stated, upTo: Today));
    }

    /// <summary>
    /// Коли рік із номера прочитати не вдалося, мовчимо. Відхиляти оголошення
    /// через те, що МИ чогось не розібрали, — найгірше з можливого.
    /// </summary>
    [Theory]
    [InlineData("WVWZZZ1JZUW386752")]    // U в позиції року не використовується
    [InlineData("WVWZZZ1JZ0W386752")]    // нуль теж
    public void An_unreadable_year_blocks_nothing(string vin)
    {
        Assert.Empty(Vin.PossibleYears(vin, upTo: Today));
        Assert.True(Vin.MatchesYear(vin, 2005, upTo: Today));
    }
}
