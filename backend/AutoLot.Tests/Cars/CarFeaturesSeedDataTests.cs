using System.Text.Json;
using AutoLot.Domain.Common;
using AutoLot.Domain.Enums;
using AutoLot.Infrastructure.Cars;

namespace AutoLot.Tests.Cars;

public class CarFeaturesSeedDataTests
{
    private const string ResourceName = "AutoLot.Infrastructure.Persistence.SeedData.car-features.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly CarFeaturesSeedDocument Document = Load();

    [Fact]
    public void Every_category_in_the_file_exists_in_the_code()
    {
        // Сідер розбирає категорію через Enum.Parse: описка у файлі впала б
        // уже під час старту застосунку, а тут падає на складанні.
        foreach (var feature in Document.Features)
        {
            Assert.True(
                Enum.TryParse<FeatureCategory>(feature.Category, out _),
                $"Опція {feature.Code} має невідому категорію «{feature.Category}».");
        }
    }

    [Fact]
    public void Every_category_has_at_least_one_option()
    {
        var used = Document.Features
            .Select(feature => Enum.Parse<FeatureCategory>(feature.Category))
            .ToHashSet();

        foreach (var category in Enum.GetValues<FeatureCategory>())
        {
            Assert.True(used.Contains(category), $"Категорія {category} лишилася порожньою.");
        }
    }

    [Fact]
    public void Codes_are_unique()
    {
        var duplicates = Document.Features
            .GroupBy(feature => feature.Code, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key);

        Assert.Empty(duplicates);
    }

    [Fact]
    public void Every_option_is_translated_into_both_languages()
    {
        foreach (var feature in Document.Features)
        {
            Assert.True(
                feature.Names.ContainsKey(LanguageCodes.Ukrainian),
                $"Опція {feature.Code} без української назви.");

            Assert.True(
                feature.Names.ContainsKey(LanguageCodes.English),
                $"Опція {feature.Code} без англійської назви.");

            Assert.All(feature.Names.Values, name => Assert.False(string.IsNullOrWhiteSpace(name)));
        }
    }

    private static CarFeaturesSeedDocument Load()
    {
        using var stream = typeof(CarReferenceSeeder).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Ресурс '{ResourceName}' не знайдено.");

        return JsonSerializer.Deserialize<CarFeaturesSeedDocument>(stream, SerializerOptions)
            ?? throw new InvalidOperationException("Файл опцій порожній.");
    }
}
