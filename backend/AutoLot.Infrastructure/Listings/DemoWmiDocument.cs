namespace AutoLot.Infrastructure.Listings;

/// <summary>
/// Опис файла demo-wmi.json: слаг марки → перші три символи VIN.
///
/// Тип внутрішній (internal), бо поза складанням демонстраційних номерів він
/// нікому не потрібен. Тести бачать його адресно — через InternalsVisibleTo
/// у .csproj: вміст сід-файлів тут перевіряється тестами.
/// </summary>
internal sealed class DemoWmiDocument
{
    public Dictionary<string, string> Prefixes { get; init; } = [];
}
