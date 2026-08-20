namespace AutoLot.Domain.Common;

/// <summary>
/// Базовий тип для всіх сутностей домену. Ключ — <see cref="long"/>:
/// оголошень і ставок з часом стає багато, а int довелося б мігрувати.
/// </summary>
public abstract class Entity
{
    public long Id { get; set; }
}
