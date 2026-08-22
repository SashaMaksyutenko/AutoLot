using AutoLot.Application.Cars;
using AutoLot.Application.Cars.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutoLot.Api.Controllers;

/// <summary>
/// Довідники автомобіля для форм і фільтрів. Назви кузовів, палив, коробок,
/// приводів і кольорів приходять мовою із заголовка Accept-Language; марки,
/// моделі та покоління не перекладаються. Дані публічні.
/// </summary>
[ApiController]
[Route("api/cars")]
[AllowAnonymous]
public sealed class CarsController(ICarCatalog carCatalog) : ControllerBase
{
    /// <summary>Усі п'ять перелічень одним запитом — саме так їх потребує панель фільтрів.</summary>
    [HttpGet("attributes")]
    [ProducesResponseType<CarAttributes>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAttributes(CancellationToken cancellationToken)
    {
        return Ok(await carCatalog.GetAttributesAsync(cancellationToken));
    }

    /// <summary>Марки: спершу популярні, далі за абеткою.</summary>
    [HttpGet("makes")]
    [ProducesResponseType<IReadOnlyList<MakeItem>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMakes(CancellationToken cancellationToken)
    {
        return Ok(await carCatalog.GetMakesAsync(cancellationToken));
    }

    [HttpGet("makes/{makeId:long}/models")]
    [ProducesResponseType<IReadOnlyList<ModelItem>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetModels(long makeId, CancellationToken cancellationToken)
    {
        return Ok(await carCatalog.GetModelsAsync(makeId, cancellationToken));
    }

    /// <summary>
    /// Покоління моделі, найновіші першими. Порожній список — нормальна
    /// відповідь: покоління заповнені не для всіх моделей.
    /// </summary>
    [HttpGet("models/{modelId:long}/generations")]
    [ProducesResponseType<IReadOnlyList<GenerationItem>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGenerations(long modelId, CancellationToken cancellationToken)
    {
        return Ok(await carCatalog.GetGenerationsAsync(modelId, cancellationToken));
    }
}
