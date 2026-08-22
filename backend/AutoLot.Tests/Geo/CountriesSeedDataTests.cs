using System.Text.Json;
using AutoLot.Domain.Common;
using AutoLot.Infrastructure.Geo;

namespace AutoLot.Tests.Geo;

public class CountriesSeedDataTests
{
    private const string ResourceName = "AutoLot.Infrastructure.Persistence.SeedData.countries.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly CountriesSeedDocument Document = Load();

    [Fact]
    public void Codes_follow_iso_3166_1_alpha_2()
    {
        // Колонка в базі — рівно два символи фіксованої довжини, тож довший
        // код навіть не збережеться.
        foreach (var country in Document.Countries)
        {
            Assert.True(
                country.Code.Length == 2 && country.Code.All(char.IsAsciiLetterUpper),
                $"Код «{country.Code}» не схожий на ISO 3166-1 alpha-2.");
        }
    }

    [Fact]
    public void Codes_are_unique()
    {
        var duplicates = Document.Countries
            .GroupBy(country => country.Code, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key);

        Assert.Empty(duplicates);
    }

    [Fact]
    public void Every_country_is_translated_into_both_languages()
    {
        foreach (var country in Document.Countries)
        {
            Assert.True(
                country.Names.ContainsKey(LanguageCodes.Ukrainian),
                $"Країна {country.Code} без української назви.");

            Assert.True(
                country.Names.ContainsKey(LanguageCodes.English),
                $"Країна {country.Code} без англійської назви.");

            Assert.All(country.Names.Values, name => Assert.False(string.IsNullOrWhiteSpace(name)));
        }
    }

    [Fact]
    public void Includes_ukraine_and_the_main_import_sources()
    {
        var codes = Document.Countries.Select(country => country.Code).ToHashSet(StringComparer.Ordinal);

        foreach (var expected in new[] { "UA", "DE", "US", "PL", "JP", "KR" })
        {
            Assert.True(codes.Contains(expected), $"У довіднику немає країни {expected}.");
        }
    }

    private static CountriesSeedDocument Load()
    {
        using var stream = typeof(GeographySeeder).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Ресурс '{ResourceName}' не знайдено.");

        return JsonSerializer.Deserialize<CountriesSeedDocument>(stream, SerializerOptions)
            ?? throw new InvalidOperationException("Файл країн порожній.");
    }
}
