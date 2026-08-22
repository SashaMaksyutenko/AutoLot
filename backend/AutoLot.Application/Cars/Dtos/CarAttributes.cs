namespace AutoLot.Application.Cars.Dtos;

/// <summary>
/// Усі п'ять довідників одним пакетом. Панель фільтрів потребує їх водночас,
/// тож п'ять окремих запитів були б зайвим марнуванням часу.
/// </summary>
public sealed record CarAttributes(
    IReadOnlyList<LookupItem> BodyTypes,
    IReadOnlyList<LookupItem> FuelTypes,
    IReadOnlyList<LookupItem> Transmissions,
    IReadOnlyList<LookupItem> DriveTypes,
    IReadOnlyList<LookupItem> Colors);
