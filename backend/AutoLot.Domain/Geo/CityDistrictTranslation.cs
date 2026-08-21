using AutoLot.Domain.Common;

namespace AutoLot.Domain.Geo;

public sealed class CityDistrictTranslation : Translation
{
    public long CityDistrictId { get; set; }

    public CityDistrict CityDistrict { get; set; } = null!;
}
