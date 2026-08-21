using AutoLot.Application.Users.Dtos;
using FluentValidation;

namespace AutoLot.Application.Users.Validation;

public sealed class UpdateLocationRequestValidator : AbstractValidator<UpdateLocationRequest>
{
    public UpdateLocationRequestValidator()
    {
        RuleFor(request => request.CityId)
            .GreaterThan(0).WithMessage("Ідентифікатор міста некоректний.")
            .When(request => request.CityId.HasValue);

        RuleFor(request => request.CityDistrictId)
            .GreaterThan(0).WithMessage("Ідентифікатор району міста некоректний.")
            .When(request => request.CityDistrictId.HasValue);

        // Район міста без міста змісту не має.
        RuleFor(request => request.CityDistrictId)
            .Null()
            .When(request => !request.CityId.HasValue)
            .WithMessage("Спершу вкажіть місто.");
    }
}
