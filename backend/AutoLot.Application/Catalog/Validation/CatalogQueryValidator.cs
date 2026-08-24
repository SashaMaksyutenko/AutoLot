using FluentValidation;

namespace AutoLot.Application.Catalog.Validation;

/// <summary>
/// Межі на параметри пошуку. Головне тут — розмір сторінки: без верхньої
/// межі один запит із PageSize=100000 витягнув би всю базу.
/// </summary>
public sealed class CatalogQueryValidator : AbstractValidator<CatalogQuery>
{
    public const int MaxPageSize = 60;

    public CatalogQueryValidator()
    {
        RuleFor(query => query.Page)
            .GreaterThan(0).WithMessage("Номер сторінки починається з одиниці.");

        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, MaxPageSize)
            .WithMessage($"Розмір сторінки має бути від 1 до {MaxPageSize}.");

        RuleFor(query => query.Text)
            .MaximumLength(120).WithMessage("Пошуковий запит задовгий.");

        RuleFor(query => query.Sort).IsInEnum().WithMessage("Невідомий порядок сортування.");

        RuleFor(query => query.PriceCurrency).IsInEnum().WithMessage("Невідома валюта.");

        // Переплутані місцями межі — найчастіша помилка у формі фільтрів,
        // і мовчки віддавати порожній список за неї не варто.
        RuleFor(query => query)
            .Must(query => NotInverted(query.PriceFrom, query.PriceTo))
            .WithName("Price").WithMessage("Нижня межа ціни більша за верхню.")
            .Must(query => NotInverted(query.YearFrom, query.YearTo))
            .WithName("Year").WithMessage("Нижня межа року більша за верхню.")
            .Must(query => NotInverted(query.MileageFrom, query.MileageTo))
            .WithName("Mileage").WithMessage("Нижня межа пробігу більша за верхню.")
            .Must(query => NotInverted(query.EngineVolumeFrom, query.EngineVolumeTo))
            .WithName("EngineVolume").WithMessage("Нижня межа об'єму більша за верхню.")
            .Must(query => NotInverted(query.PowerFrom, query.PowerTo))
            .WithName("Power").WithMessage("Нижня межа потужності більша за верхню.");

        RuleForEach(query => query.FeatureIds)
            .GreaterThan(0).WithMessage("Некоректна опція комплектації.");
    }

    private static bool NotInverted<TValue>(TValue? from, TValue? to)
        where TValue : struct, IComparable<TValue> =>
        from is null || to is null || from.Value.CompareTo(to.Value) <= 0;
}
