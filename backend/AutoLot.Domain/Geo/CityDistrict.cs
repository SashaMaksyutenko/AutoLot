using AutoLot.Domain.Common;

namespace AutoLot.Domain.Geo;

/// <summary>
/// Район усередині міста — Оболонський, Сихівський тощо. Є лише у великих
/// містах, тож у профілі та в оголошенні це поле необов'язкове.
/// </summary>
public sealed class CityDistrict : Entity
{
    public long CityId { get; set; }

    public City City { get; set; } = null!;

    public string Code { get; set; } = string.Empty;

    public ICollection<CityDistrictTranslation> Translations { get; } = [];
}
