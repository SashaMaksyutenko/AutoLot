using AutoLot.Domain.Common;

namespace AutoLot.Domain.Geo;

/// <summary>
/// Країна — потрібна двічі: як країна-виробник і як країна, звідки авто
/// пригнали. Назва перекладається, тож це таблиця, а не enum.
/// </summary>
public sealed class Country : Entity
{
    /// <summary>Код за ISO 3166-1 alpha-2: «DE», «US», «JP».</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Найчастіші країни показуємо вгорі списку.</summary>
    public int SortOrder { get; set; }

    public ICollection<CountryTranslation> Translations { get; } = [];
}
