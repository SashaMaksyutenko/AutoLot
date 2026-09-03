using AutoLot.Domain.Common;

namespace AutoLot.Domain.Billing;

/// <summary>
/// Тарифний план (SPEC §12).
///
/// План — довідник, а не код: назви й ціни живуть у сід-файлі, тож змінити
/// вартість або додати тариф можна без правки застосунку. Саме тому ліміт
/// оголошень тут **властивість плану**, а не константа в сервісі: раніше
/// «п'ять» було вписане просто в код, і будь-яка зміна означала збірку.
/// </summary>
public sealed class Plan : Entity
{
    /// <summary>Сталий код для сіду: «free», «plus», «pro».</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Скільки коштує за один період. Нуль — безкоштовний тариф.</summary>
    public decimal Price { get; set; }

    /// <summary>Скільки днів діє одна оплата.</summary>
    public int DurationDays { get; set; } = 30;

    /// <summary>
    /// Скільки активних оголошень дозволено. <c>null</c> означає «без межі» —
    /// і це не те саме, що нуль. Нуль забороняв би публікувати взагалі.
    /// </summary>
    public int? ListingLimit { get; set; }

    /// <summary>
    /// План, який діє без оплати. Такий має бути рівно один: саме на нього
    /// спирається кожен, хто нічого не купував.
    /// </summary>
    public bool IsDefault { get; set; }

    public int SortOrder { get; set; }

    public ICollection<PlanTranslation> Translations { get; } = [];
}

/// <summary>Назва й опис плану однією мовою.</summary>
public sealed class PlanTranslation : Translation
{
    public long PlanId { get; set; }

    public Plan Plan { get; set; } = null!;

    /// <summary>Рядок під назвою: чим цей тариф кращий за попередній.</summary>
    public string Description { get; set; } = string.Empty;
}
