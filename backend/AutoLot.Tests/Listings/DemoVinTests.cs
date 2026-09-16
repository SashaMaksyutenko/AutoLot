using AutoLot.Domain.Listings;
using AutoLot.Infrastructure.Listings;
using AutoLot.Infrastructure.Cars;
using AutoLot.Infrastructure.Persistence;

namespace AutoLot.Tests.Listings;

/// <summary>
/// Номери кузова в демонстраційних даних.
///
/// Головна вимога до них одна: вони мають проходити ВЛАСНІ перевірки проєкту.
/// Демо-дані, які не пройшли б валідацію справжнього оголошення, показують
/// сайт таким, яким він не є, — і найгірше, що це помічається не одразу, а
/// коли хтось спробує повторити те саме руками.
/// </summary>
public class DemoVinTests
{
    private const string ResourceName = "AutoLot.Infrastructure.Persistence.SeedData.demo-wmi.json";

    private static readonly IReadOnlyDictionary<string, string> Prefixes =
        SeedResource.ReadAsync<DemoWmiDocument>(ResourceName).GetAwaiter().GetResult().Prefixes;

    /// <summary>Роки, які трапляються в демо-даних: сідер бере від 2005-го.</summary>
    private static readonly int[] Years = [2005, 2012, 2019, 2021, 2024, 2026];

    [Fact]
    public void The_reference_of_prefixes_is_not_empty()
    {
        Assert.NotEmpty(Prefixes);
    }

    /// <summary>
    /// Префікс — рівно три символи з тих, що дозволені у VIN. Помилка тут
    /// зробила б непридатним КОЖЕН номер цієї марки.
    /// </summary>
    [Fact]
    public void Every_prefix_could_start_a_real_vin()
    {
        Assert.All(Prefixes.Values, prefix =>
        {
            Assert.Equal(3, prefix.Length);
            Assert.DoesNotContain(prefix, symbol => symbol is 'I' or 'O' or 'Q');
            Assert.All(prefix, symbol => Assert.True(char.IsAsciiLetterUpper(symbol) || char.IsAsciiDigit(symbol)));
        });
    }

    /// <summary>
    /// Кожна марка з цього файла має існувати в довіднику марок.
    ///
    /// Помилка тут найтихіша з можливих: описка в слазі не ламає нічого —
    /// просто ця марка назавжди лишається без номерів, і помітити це можна
    /// хіба що переглянувши двісті оголошень підряд.
    /// </summary>
    [Fact]
    public async Task Every_make_here_exists_in_the_car_reference()
    {
        var reference = await SeedResource.ReadAsync<CarMakesSeedDocument>(
            "AutoLot.Infrastructure.Persistence.SeedData.car-makes.json");

        var known = reference.Makes
            .Select(make => make.Slug)
            .ToHashSet(StringComparer.Ordinal);

        var unknown = Prefixes.Keys.Where(slug => !known.Contains(slug)).ToList();

        Assert.True(unknown.Count == 0, "немає таких марок: " + string.Join(", ", unknown));
    }

    /// <summary>
    /// Найважливіший тест: кожен зібраний номер має бути таким, який прийняв
    /// би валідатор справжнього оголошення — з правильною формою, узгодженим
    /// роком і, де треба, зі зведеною контрольною цифрою.
    /// </summary>
    [Fact]
    public void Every_generated_vin_would_pass_the_real_validation()
    {
        var random = new Random(20260916);

        foreach (var slug in Prefixes.Keys)
        {
            foreach (var year in Years)
            {
                var vin = DemoVins.Create(Prefixes, slug, year, random);

                Assert.NotNull(vin);
                Assert.True(Vin.HasValidShape(vin), $"{slug} {year}: {vin}");
                Assert.True(Vin.MatchesYear(vin, year, upTo: 2030), $"{slug} {year}: {vin}");

                if (Vin.RequiresCheckDigit(vin))
                {
                    Assert.True(Vin.IsCheckDigitValid(vin), $"{slug} {year}: {vin}");
                }
            }
        }
    }

    /// <summary>
    /// Марка, якої в довіднику немає, лишається без номера — і це нормально:
    /// поле необов'язкове. Вигадувати префікс наосліп було б гірше, ніж
    /// чесно нічого не показати.
    /// </summary>
    [Fact]
    public void An_unknown_make_gets_no_vin()
    {
        Assert.Null(DemoVins.Create(Prefixes, "no-such-make", 2020, new Random(1)));
    }

    /// <summary>
    /// До 1981-го сімнадцятизначних номерів не існувало, тож і вигадувати їх
    /// для старих авто нема сенсу.
    /// </summary>
    [Fact]
    public void A_car_older_than_the_standard_gets_no_vin()
    {
        Assert.Null(DemoVins.Create(Prefixes, Prefixes.Keys.First(), 1975, new Random(1)));
    }

    /// <summary>
    /// Номери мають різнитися: однаковий VIN у двох оголошень — саме те, що
    /// проєкт вважає ознакою шахрайства.
    /// </summary>
    [Fact]
    public void Two_cars_of_one_make_get_different_numbers()
    {
        var random = new Random(7);
        var slug = Prefixes.Keys.First();

        var numbers = Enumerable
            .Range(0, 200)
            .Select(_ => DemoVins.Create(Prefixes, slug, 2021, random))
            .ToList();

        Assert.Equal(numbers.Count, numbers.Distinct(StringComparer.Ordinal).Count());
    }
}
