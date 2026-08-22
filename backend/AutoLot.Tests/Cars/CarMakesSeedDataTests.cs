using System.Text.Json;
using System.Text.RegularExpressions;
using AutoLot.Infrastructure.Cars;

namespace AutoLot.Tests.Cars;

public partial class CarMakesSeedDataTests
{
    private const string ResourceName = "AutoLot.Infrastructure.Persistence.SeedData.car-makes.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly CarMakesSeedDocument Document = Load();

    [Fact]
    public void Covers_at_least_forty_makes()
    {
        // SPEC §3: сід приблизно на 40 популярних марок.
        Assert.True(Document.Makes.Count >= 40, $"Марок лише {Document.Makes.Count}.");
    }

    [Fact]
    public void Make_slugs_are_unique()
    {
        var duplicates = Document.Makes
            .GroupBy(make => make.Slug, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key);

        Assert.Empty(duplicates);
    }

    [Fact]
    public void Model_slugs_are_unique_inside_their_make()
    {
        foreach (var make in Document.Makes)
        {
            var duplicates = make.Models
                .GroupBy(model => model.Slug, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToList();

            Assert.True(duplicates.Count == 0, $"У марки {make.Slug} повторені моделі: {string.Join(", ", duplicates)}");
        }
    }

    [Fact]
    public void Every_make_has_models()
    {
        var empty = Document.Makes.Where(make => make.Models.Count == 0).Select(make => make.Slug);

        Assert.Empty(empty);
    }

    [Fact]
    public void Slugs_are_url_safe()
    {
        foreach (var make in Document.Makes)
        {
            AssertSlug(make.Slug);

            foreach (var model in make.Models)
            {
                AssertSlug(model.Slug);

                foreach (var generation in model.Generations)
                {
                    AssertSlug(generation.Slug);
                }
            }
        }
    }

    [Fact]
    public void Generation_years_make_sense()
    {
        foreach (var make in Document.Makes)
        {
            foreach (var model in make.Models)
            {
                foreach (var generation in model.Generations)
                {
                    var label = $"{make.Slug}/{model.Slug}/{generation.Slug}";

                    Assert.True(
                        generation.YearFrom >= 1950 && generation.YearFrom <= 2030,
                        $"{label}: рік початку {generation.YearFrom} виглядає помилковим.");

                    if (generation.YearTo is { } yearTo)
                    {
                        Assert.True(
                            yearTo >= generation.YearFrom,
                            $"{label}: покоління завершилося раніше, ніж почалося.");
                    }
                }
            }
        }
    }

    [Fact]
    public void Nothing_is_left_unnamed()
    {
        foreach (var make in Document.Makes)
        {
            Assert.False(string.IsNullOrWhiteSpace(make.Name));

            Assert.All(make.Models, model => Assert.False(string.IsNullOrWhiteSpace(model.Name)));
        }
    }

    private static void AssertSlug(string slug)
    {
        Assert.True(SlugPattern().IsMatch(slug), $"Slug «{slug}» містить неприпустимі символи.");
    }

    /// <summary>Малі латинські літери, цифри й дефіси — усе, що безпечно в URL.</summary>
    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex SlugPattern();

    private static CarMakesSeedDocument Load()
    {
        using var stream = typeof(CarReferenceSeeder).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Ресурс '{ResourceName}' не знайдено.");

        return JsonSerializer.Deserialize<CarMakesSeedDocument>(stream, SerializerOptions)
            ?? throw new InvalidOperationException("Файл марок порожній.");
    }
}
