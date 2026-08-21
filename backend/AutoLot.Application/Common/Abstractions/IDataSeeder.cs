namespace AutoLot.Application.Common.Abstractions;

/// <summary>
/// Наповнювач початкових даних. Кожен такий клас відповідає за свій шматок
/// (географія, ролі, довідники авто) і мусить бути ідемпотентним: повторний
/// запуск нічого не дублює.
/// </summary>
public interface IDataSeeder
{
    /// <summary>Менше значення — раніший запуск. Потрібно там, де дані залежні.</summary>
    int Order { get; }

    Task SeedAsync(CancellationToken cancellationToken = default);
}
