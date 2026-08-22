using AutoLot.Domain.Common;

namespace AutoLot.Domain.Cars;

public sealed class FeatureTranslation : Translation
{
    public long FeatureId { get; set; }

    public Feature Feature { get; set; } = null!;
}
