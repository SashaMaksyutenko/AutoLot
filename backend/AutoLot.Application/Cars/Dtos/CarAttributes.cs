namespace AutoLot.Application.Cars.Dtos;

/// <summary>
/// Усі довідники характеристик одним пакетом. Панель фільтрів потребує їх
/// водночас, тож окремі запити на кожен були б зайвим марнуванням часу.
/// </summary>
public sealed record CarAttributes(
    IReadOnlyList<LookupItem> BodyTypes,
    IReadOnlyList<LookupItem> FuelTypes,
    IReadOnlyList<LookupItem> Transmissions,
    IReadOnlyList<LookupItem> DriveTypes,
    IReadOnlyList<LookupItem> Colors,

    /// <summary>Новий чи вживаний. Теж довідник, бо назви теж перекладаються.</summary>
    IReadOnlyList<LookupItem> Conditions,

    /// <summary>Стан пошкоджень: цілий, битий, на запчастини.</summary>
    IReadOnlyList<LookupItem> DamageStates,

    /// <summary>Стан фарби: заводська, часткове, повне фарбування.</summary>
    IReadOnlyList<LookupItem> PaintConditions,

    /// <summary>Євро-1…Євро-6. Впливає на розмитнення, тож питають часто.</summary>
    IReadOnlyList<LookupItem> EcologyStandards,

    /// <summary>Тип зарядного роз'єму. Має сенс лише для електромобілів.</summary>
    IReadOnlyList<LookupItem> ChargingPorts);
