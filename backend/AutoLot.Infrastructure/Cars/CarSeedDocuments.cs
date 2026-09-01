using AutoLot.Infrastructure.Persistence;

namespace AutoLot.Infrastructure.Cars;

/// <summary>
/// Форма файла car-attributes.json. Власних полів не має: файл нічим не
/// відрізняється від будь-якого іншого переліку назв перелічень. Окремий тип
/// лишається заради назви — вона каже, який саме файл читають.
/// </summary>
internal sealed record CarAttributesSeedDocument : EnumSeedDocument;

/// <summary>Форма файла car-makes.json.</summary>
internal sealed record CarMakesSeedDocument
{
    public IReadOnlyList<MakeSeed> Makes { get; init; } = [];
}

internal sealed record MakeSeed
{
    public string Name { get; init; } = string.Empty;

    public string Slug { get; init; } = string.Empty;

    public bool IsPopular { get; init; }

    public IReadOnlyList<ModelSeed> Models { get; init; } = [];
}

internal sealed record ModelSeed
{
    public string Name { get; init; } = string.Empty;

    public string Slug { get; init; } = string.Empty;

    public IReadOnlyList<GenerationSeed> Generations { get; init; } = [];
}

internal sealed record GenerationSeed
{
    public string Name { get; init; } = string.Empty;

    public string Slug { get; init; } = string.Empty;

    public int YearFrom { get; init; }

    public int? YearTo { get; init; }
}

/// <summary>Форма файла car-features.json.</summary>
internal sealed record CarFeaturesSeedDocument
{
    public IReadOnlyList<FeatureSeed> Features { get; init; } = [];
}

internal sealed record FeatureSeed
{
    public string Code { get; init; } = string.Empty;

    /// <summary>Назва значення <c>FeatureCategory</c>: «Interior», «Safety» тощо.</summary>
    public string Category { get; init; } = string.Empty;

    public Dictionary<string, string> Names { get; init; } = [];
}
