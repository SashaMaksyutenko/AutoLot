using AutoLot.Domain.Common;

namespace AutoLot.Domain.Geo;

public sealed class CityTranslation : Translation
{
    public long CityId { get; set; }

    public City City { get; set; } = null!;
}
