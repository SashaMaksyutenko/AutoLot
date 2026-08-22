using AutoLot.Domain.Common;

namespace AutoLot.Domain.Cars;

/// <summary>
/// Марка автомобіля. Не перекладається (SPEC §6): «Volkswagen» лишається
/// «Volkswagen» обома мовами.
/// </summary>
public sealed class Make : Entity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Ім'я для URL, наприклад «mercedes-benz». Заодно сталий ключ для сіду.</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>Популярні марки показуємо окремим блоком угорі списку.</summary>
    public bool IsPopular { get; set; }

    public ICollection<Model> Models { get; } = [];
}
