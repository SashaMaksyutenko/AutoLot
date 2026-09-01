using System.Text.Json;
using AutoLot.Domain.Common;
using AutoLot.Domain.Enums;
using AutoLot.Infrastructure.Listings;

namespace AutoLot.Tests.Listings;

/// <summary>
/// Стежить, щоб файл назв причин і перелічення в коді не розійшлися. Помилка
/// тут найлегша з можливих: хтось додає причину в enum і забуває назву —
/// у списку з'являється сире «Duplicate», і ніхто цього не помічає, доки не
/// побачить на сайті.
/// </summary>
public class ModerationSeedDataTests
{
    private const string ResourceName = "AutoLot.Infrastructure.Persistence.SeedData.moderation.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly SeedDocument Document = Load();

    [Fact]
    public void Every_reason_in_the_code_is_named_in_the_file()
    {
        var named = Reasons().Select(reason => reason.Value);

        Assert.Equal(
            Enum.GetNames<ListingReportReason>().OrderBy(name => name, StringComparer.Ordinal),
            named.OrderBy(name => name, StringComparer.Ordinal));
    }

    [Fact]
    public void Every_reason_is_named_in_both_languages()
    {
        foreach (var reason in Reasons())
        {
            foreach (var code in LanguageCodes.Supported)
            {
                Assert.True(
                    reason.Names.ContainsKey(code),
                    $"Причина {reason.Value} не має назви мовою «{code}».");
            }
        }
    }

    [Fact]
    public void No_name_is_left_blank()
    {
        foreach (var reason in Reasons())
        {
            Assert.All(reason.Names.Values, name => Assert.False(string.IsNullOrWhiteSpace(name)));
        }
    }

    [Fact]
    public void Other_comes_last()
    {
        // Порядок у файлі стає порядком у списку. «Інше» першим пунктом
        // зібрало б усі скарги на себе — люди тиснуть верхній.
        Assert.Equal(nameof(ListingReportReason.Other), Reasons()[^1].Value);
    }

    private static IReadOnlyList<ValueSeed> Reasons() =>
        Document.Enums[nameof(ListingReportReason)];

    private static SeedDocument Load()
    {
        using var stream = typeof(ModerationSeeder).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Ресурс '{ResourceName}' не знайдено.");

        return JsonSerializer.Deserialize<SeedDocument>(stream, SerializerOptions)
            ?? throw new InvalidOperationException("Файл довідників модерації порожній.");
    }

    /// <summary>
    /// Форма файла для тесту. Власна, а не та, якою користується сідер:
    /// тест має читати файл своїми очима, інакше однакова помилка в обох
    /// місцях зійшлася б і лишилася непоміченою.
    /// </summary>
    private sealed record SeedDocument
    {
        public Dictionary<string, IReadOnlyList<ValueSeed>> Enums { get; init; } = [];
    }

    private sealed record ValueSeed
    {
        public string Value { get; init; } = string.Empty;

        public Dictionary<string, string> Names { get; init; } = [];
    }
}
