using AutoLot.Application.Users.Dtos;
using FluentValidation;

namespace AutoLot.Application.Users.Validation;

/// <summary>
/// Правила ті самі, що при реєстрації: вимоги до імені й телефону не мусять
/// залежати від того, у якій формі їх вводять.
/// </summary>
public sealed class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    private const string PhonePattern = @"^\+380\d{9}$";

    public UpdateProfileRequestValidator()
    {
        RuleFor(request => request.DisplayName)
            .NotEmpty().WithMessage("Вкажіть, як до вас звертатися.")
            .MinimumLength(2).WithMessage("Ім'я закоротке.")
            .MaximumLength(100).WithMessage("Ім'я задовге.");

        // Порожній телефон дозволений: це спосіб його прибрати.
        RuleFor(request => request.PhoneNumber)
            .Matches(PhonePattern).WithMessage("Телефон має бути у форматі +380XXXXXXXXX.")
            .When(request => !string.IsNullOrWhiteSpace(request.PhoneNumber));
    }
}
