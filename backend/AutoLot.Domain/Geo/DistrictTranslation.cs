using AutoLot.Domain.Common;

namespace AutoLot.Domain.Geo;

public sealed class DistrictTranslation : Translation
{
    public long DistrictId { get; set; }

    public District District { get; set; } = null!;
}
