using FluentValidation;
using AutoLot.Application.Common.Localization;

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
            .GreaterThan(0).WithMessage(MessageCodes.CatalogPageMin);

        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, MaxPageSize)
            .WithMessage($"Розмір сторінки має бути від 1 до {MaxPageSize}.");

        RuleFor(query => query.Text)
            .MaximumLength(120).WithMessage(MessageCodes.CatalogTextTooLong);

        RuleFor(query => query.Sort).IsInEnum().WithMessage(MessageCodes.CatalogSortUnknown);

        RuleFor(query => query.PriceCurrency).IsInEnum().WithMessage(MessageCodes.ListingCurrencyUnknown);

        // Переплутані місцями межі — найчастіша помилка у формі фільтрів,
        // і мовчки віддавати порожній список за неї не варто.
        RuleFor(query => query)
            .Must(query => NotInverted(query.PriceFrom, query.PriceTo))
            .WithName("Price").WithMessage(MessageCodes.CatalogPriceRange)
            .Must(query => NotInverted(query.YearFrom, query.YearTo))
            .WithName("Year").WithMessage(MessageCodes.CatalogYearRange)
            .Must(query => NotInverted(query.MileageFrom, query.MileageTo))
            .WithName("Mileage").WithMessage(MessageCodes.CatalogMileageRange)
            .Must(query => NotInverted(query.EngineVolumeFrom, query.EngineVolumeTo))
            .WithName("EngineVolume").WithMessage(MessageCodes.CatalogEngineRange)
            .Must(query => NotInverted(query.PowerFrom, query.PowerTo))
            .WithName("Power").WithMessage(MessageCodes.CatalogPowerRange);

        RuleForEach(query => query.FeatureIds)
            .GreaterThan(0).WithMessage(MessageCodes.CarFeaturesInvalid);
    }

    private static bool NotInverted<TValue>(TValue? from, TValue? to)
        where TValue : struct, IComparable<TValue> =>
        from is null || to is null || from.Value.CompareTo(to.Value) <= 0;
}
