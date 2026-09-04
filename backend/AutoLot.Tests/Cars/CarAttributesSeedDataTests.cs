using System.Text.Json;
using AutoLot.Domain.Common;
using AutoLot.Domain.Enums;
using AutoLot.Infrastructure.Cars;

namespace AutoLot.Tests.Cars;

/// <summary>
/// Стежить, щоб файл перекладів і перелічення в коді не розійшлися. Це
/// найлегша помилка в такій схемі: хтось додає значення в enum і забуває
/// назву — у списку з'являється порожній рядок, і ніхто цього не помічає,
/// доки не побачить на сайті.
/// </summary>
public class CarAttributesSeedDataTests
{
    private const string ResourceName = "AutoLot.Infrastructure.Persistence.SeedData.car-attributes.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly Dictionary<string, Type> EnumTypes = new(StringComparer.Ordinal)
    {
        [nameof(BodyType)] = typeof(BodyType),
        [nameof(FuelType)] = typeof(FuelType),
        [nameof(TransmissionType)] = typeof(TransmissionType),
        [nameof(DrivetrainType)] = typeof(DrivetrainType),
        [nameof(CarColor)] = typeof(CarColor),
        [nameof(CarCondition)] = typeof(CarCondition),
        [nameof(DamageState)] = typeof(DamageState),
        [nameof(PaintCondition)] = typeof(PaintCondition),
        [nameof(EcologyStandard)] = typeof(EcologyStandard),
        [nameof(ChargingPortType)] = typeof(ChargingPortType),
    };

    private static readonly CarAttributesSeedDocument Document = Load();

    [Fact]
    public void Describes_every_enum_the_catalog_serves()
    {
        Assert.Equal(
            EnumTypes.Keys.OrderBy(name => name, StringComparer.Ordinal),
            Document.Enums.Keys.OrderBy(name => name, StringComparer.Ordinal));
    }

    [Fact]
    public void Every_value_in_the_file_exists_in_the_code()
    {
        foreach (var (enumName, values) in Document.Enums)
        {
            var known = Enum.GetNames(EnumTypes[enumName]);

            foreach (var value in values)
            {
                Assert.Contains(
                    value.Value,
                    known);
            }
        }
    }

    [Fact]
    public void Every_value_in_the_code_is_named_in_the_file()
    {
        foreach (var (enumName, type) in EnumTypes)
        {
            var described = Document.Enums[enumName]
                .Select(value => value.Value)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var name in Enum.GetNames(type))
            {
                Assert.True(described.Contains(name), $"{enumName}.{name} не має назви у файлі перекладів.");
            }
        }
    }

    [Fact]
    public void Every_value_is_translated_into_both_languages()
    {
        foreach (var (enumName, values) in Document.Enums)
        {
            foreach (var value in values)
            {
                Assert.True(
                    value.Names.ContainsKey(LanguageCodes.Ukrainian),
                    $"{enumName}.{value.Value} без української назви.");

                Assert.True(
                    value.Names.ContainsKey(LanguageCodes.English),
                    $"{enumName}.{value.Value} без англійської назви.");

                Assert.All(value.Names.Values, name => Assert.False(string.IsNullOrWhiteSpace(name)));
            }
        }
    }

    [Fact]
    public void No_value_is_listed_twice()
    {
        foreach (var (enumName, values) in Document.Enums)
        {
            var distinct = values.Select(value => value.Value).Distinct(StringComparer.Ordinal).Count();

            Assert.True(distinct == values.Count, $"У {enumName} є повторені значення.");
        }
    }

    private static CarAttributesSeedDocument Load()
    {
        using var stream = typeof(CarReferenceSeeder).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Ресурс '{ResourceName}' не знайдено.");

        return JsonSerializer.Deserialize<CarAttributesSeedDocument>(stream, SerializerOptions)
            ?? throw new InvalidOperationException("Файл довідників авто порожній.");
    }
}
