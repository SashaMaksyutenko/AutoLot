using AutoLot.Application.Cars.Dtos;

namespace AutoLot.Application.Cars;

/// <summary>
/// Довідники автомобіля. Назви кузовів, палив і кольорів — мовою запиту;
/// марки й моделі не перекладаються (SPEC §6).
/// </summary>
public interface ICarCatalog
{
    Task<CarAttributes> GetAttributesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MakeItem>> GetMakesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ModelItem>> GetModelsAsync(
        long makeId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GenerationItem>> GetGenerationsAsync(
        long modelId,
        CancellationToken cancellationToken = default);
}
