using AutoLot.Domain.Common;
using AutoLot.Domain.Enums;

namespace AutoLot.Domain.Cars;

/// <summary>
/// Опція комплектації: підігрів сидінь, парктроніки, фаркоп тощо.
///
/// Чому це таблиця, а не сорок булевих колонок у Car: кожна нова опція
/// інакше означала б міграцію, а фільтр довелося б писати окремо під кожну.
/// Тут нова опція — рядок у сід-файлі, а фільтр один на всі.
/// </summary>
public sealed class Feature : Entity
{
    /// <summary>Сталий код для сіду: «heated-seats», «tow-bar».</summary>
    public string Code { get; set; } = string.Empty;

    public FeatureCategory Category { get; set; }

    public int SortOrder { get; set; }

    public ICollection<FeatureTranslation> Translations { get; } = [];
}
