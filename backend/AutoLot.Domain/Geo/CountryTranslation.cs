using AutoLot.Domain.Common;

namespace AutoLot.Domain.Geo;

public sealed class CountryTranslation : Translation
{
    public long CountryId { get; set; }

    public Country Country { get; set; } = null!;
}
