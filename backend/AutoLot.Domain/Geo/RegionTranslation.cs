using AutoLot.Domain.Common;

namespace AutoLot.Domain.Geo;

public sealed class RegionTranslation : Translation
{
    public long RegionId { get; set; }

    public Region Region { get; set; } = null!;
}
