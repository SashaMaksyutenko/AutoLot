using AutoLot.Domain.Common;

namespace AutoLot.Domain.Cars;

public sealed class Model : Entity
{
    public long MakeId { get; set; }

    public Make Make { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    /// <summary>Унікальний у межах марки: «audi-a4», «bmw-x5».</summary>
    public string Slug { get; set; } = string.Empty;

    public ICollection<Generation> Generations { get; } = [];
}
