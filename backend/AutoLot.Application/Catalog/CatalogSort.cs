namespace AutoLot.Application.Catalog;

/// <summary>Порядок видачі каталогу.</summary>
public enum CatalogSort
{
    /// <summary>Найновіші оголошення першими — типовий порядок.</summary>
    Newest = 0,

    /// <summary>Дешевші першими. Порівняння йде за нормалізованою гривнею.</summary>
    PriceAscending = 1,

    PriceDescending = 2,

    /// <summary>Менший пробіг першим; авто без пробігу — на початку.</summary>
    MileageAscending = 3,

    /// <summary>Свіжіший рік випуску першим.</summary>
    YearDescending = 4,
}
