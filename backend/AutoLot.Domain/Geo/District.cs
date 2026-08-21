using AutoLot.Domain.Common;

namespace AutoLot.Domain.Geo;

/// <summary>
/// Адміністративний район області — той поділ, що виник після реформи 2020 року
/// (по всій країні їх 136). Не плутати з <see cref="CityDistrict"/>, районом
/// усередині міста.
/// </summary>
public sealed class District : Entity
{
    public long RegionId { get; set; }

    public Region Region { get; set; } = null!;

    /// <summary>Сталий код для сіду, унікальний у межах області.</summary>
    public string Code { get; set; } = string.Empty;

    public ICollection<DistrictTranslation> Translations { get; } = [];

    public ICollection<City> Cities { get; } = [];
}
