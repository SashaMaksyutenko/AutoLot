using AutoLot.Domain.Cars;

namespace AutoLot.Domain.Listings;

/// <summary>
/// Зв'язок «це авто має цю опцію». Власного ключа не має — його роль грає
/// пара CarId + FeatureId, і вона ж не дає додати ту саму опцію двічі.
/// </summary>
public sealed class CarFeature
{
    public long CarId { get; set; }

    public Car Car { get; set; } = null!;

    public long FeatureId { get; set; }

    public Feature Feature { get; set; } = null!;
}
