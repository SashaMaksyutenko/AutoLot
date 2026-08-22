namespace AutoLot.Infrastructure.Cars;

/// <summary>
/// Форма файла car-attributes.json. Значення кожного перелічення — масив,
/// а не об'єкт: порядок елементів масиву визначений, і саме він стає порядком
/// у випадаючому списку.
/// </summary>
internal sealed record CarAttributesSeedDocument
{
    public Dictionary<string, IReadOnlyList<EnumValueSeed>> Enums { get; init; } = [];
}

internal sealed record EnumValueSeed
{
    public string Value { get; init; } = string.Empty;

    public Dictionary<string, string> Names { get; init; } = [];
}

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
