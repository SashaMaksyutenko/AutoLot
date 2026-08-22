using AutoLot.Domain.Common;

namespace AutoLot.Domain.Cars;

/// <summary>
/// Покоління моделі — «BMW X5 G05». Рік завершення порожній, поки покоління
/// ще випускають.
/// </summary>
public sealed class Generation : Entity
{
    public long ModelId { get; set; }

    public Model Model { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public int YearFrom { get; set; }

    public int? YearTo { get; set; }
}
