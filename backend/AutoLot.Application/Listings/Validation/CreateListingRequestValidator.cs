using AutoLot.Application.Listings.Dtos;
using FluentValidation;

namespace AutoLot.Application.Listings.Validation;

/// <summary>
/// Спільні правила для створення й редагування. Обидва запити описують те
/// саме оголошення, тож перевірки живуть в одному місці, а два валідатори
/// нижче лише підключають їх до свого типу.
/// </summary>
internal static class ListingRules
{
    public const int MaxTitleLength = 120;
    public const int MaxDescriptionLength = 5000;

    /// <summary>Верхня межа радше проти описок у нулях, ніж проти дорогих авто.</summary>
    public const decimal MaxPrice = 100_000_000m;

    public static void Apply<TRequest>(
        AbstractValidator<TRequest> validator,
        Func<TRequest, string> title,
        Func<TRequest, string> description,
        Func<TRequest, long> cityId,
        Func<TRequest, long?> cityDistrictId,
        Func<TRequest, decimal> price)
    {
        validator.RuleFor(request => title(request))
            .NotEmpty().WithName("Title").WithMessage("Вкажіть заголовок.")
            .MinimumLength(10).WithName("Title").WithMessage("Заголовок закороткий.")
            .MaximumLength(MaxTitleLength).WithName("Title").WithMessage("Заголовок задовгий.");

        validator.RuleFor(request => description(request))
            .NotEmpty().WithName("Description").WithMessage("Додайте опис.")
            .MinimumLength(20).WithName("Description").WithMessage("Опис закороткий.")
            .MaximumLength(MaxDescriptionLength).WithName("Description").WithMessage("Опис задовгий.");

        validator.RuleFor(request => cityId(request))
            .GreaterThan(0).WithName("CityId").WithMessage("Оберіть місто.");

        validator.RuleFor(request => cityDistrictId(request))
            .GreaterThan(0).WithName("CityDistrictId").WithMessage("Некоректний район міста.")
            .When(request => cityDistrictId(request).HasValue);

        validator.RuleFor(request => price(request))
            .GreaterThan(0).WithName("Price").WithMessage("Ціна має бути більшою за нуль.")
            .LessThanOrEqualTo(MaxPrice).WithName("Price").WithMessage("Ціна виглядає помилковою.");
    }
}

public sealed class CreateListingRequestValidator : AbstractValidator<CreateListingRequest>
{
    public CreateListingRequestValidator(IValidator<CarSpecification> carValidator)
    {
        ListingRules.Apply(
            this,
            request => request.Title,
            request => request.Description,
            request => request.CityId,
            request => request.CityDistrictId,
            request => request.Price);

        RuleFor(request => request.Currency).IsInEnum().WithMessage("Невідома валюта.");
        RuleFor(request => request.Type).IsInEnum().WithMessage("Невідомий тип оголошення.");

        RuleFor(request => request.Car).NotNull().SetValidator(carValidator);
    }
}

public sealed class UpdateListingRequestValidator : AbstractValidator<UpdateListingRequest>
{
    public UpdateListingRequestValidator(IValidator<CarSpecification> carValidator)
    {
        ListingRules.Apply(
            this,
            request => request.Title,
            request => request.Description,
            request => request.CityId,
            request => request.CityDistrictId,
            request => request.Price);

        RuleFor(request => request.Currency).IsInEnum().WithMessage("Невідома валюта.");

        RuleFor(request => request.Car).NotNull().SetValidator(carValidator);
    }
}

public sealed class RejectListingRequestValidator : AbstractValidator<RejectListingRequest>
{
    public RejectListingRequestValidator()
    {
        RuleFor(request => request.Reason)
            .NotEmpty().WithMessage("Вкажіть причину відмови.")
            .MinimumLength(10).WithMessage("Причина має пояснювати, що виправити.")
            .MaximumLength(500).WithMessage("Причина задовга.");
    }
}
