using AutoLot.Domain.Common;

namespace AutoLot.Domain.Geo;

/// <summary>
/// Верхній рівень географії — область, Автономна Республіка Крим або місто зі
/// спеціальним статусом (Київ, Севастополь). Усього 27 записів.
/// </summary>
public sealed class Region : Entity
{
    /// <summary>
    /// Код за ISO 3166-2, наприклад «UA-32» для Київської області. Сталий
    /// ідентифікатор: за ним сід упізнає наявний запис і не створює дубль,
    /// навіть якщо назву згодом виправлять.
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Порядок у випадаючому списку; за рівних значень — за назвою.</summary>
    public int SortOrder { get; set; }

    public ICollection<RegionTranslation> Translations { get; } = [];

    public ICollection<District> Districts { get; } = [];

    public ICollection<City> Cities { get; } = [];
}
