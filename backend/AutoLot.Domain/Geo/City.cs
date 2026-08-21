using AutoLot.Domain.Common;

namespace AutoLot.Domain.Geo;

/// <summary>
/// Населений пункт. Область зберігаємо прямо тут, а не тільки через район:
/// у міст зі спеціальним статусом району немає взагалі, та й фільтр «усі авто
/// в області» тоді обходиться без зайвого з'єднання таблиць.
/// </summary>
public sealed class City : Entity
{
    public long RegionId { get; set; }

    public Region Region { get; set; } = null!;

    /// <summary>Порожній у Києва та Севастополя — вони районам не підпорядковані.</summary>
    public long? DistrictId { get; set; }

    public District? District { get; set; }

    public string Code { get; set; } = string.Empty;

    /// <summary>Обласний центр показуємо першим у списку міст своєї області.</summary>
    public bool IsRegionCentre { get; set; }

    /// <summary>
    /// Приблизна кількість жителів. Потрібна не для статистики, а для
    /// сортування: великі міста мають бути вгорі списку, бо їх шукають частіше.
    /// </summary>
    public int Population { get; set; }

    public ICollection<CityTranslation> Translations { get; } = [];

    public ICollection<CityDistrict> CityDistricts { get; } = [];
}
