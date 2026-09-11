using AutoLot.Application.Users.Dtos;
using FluentValidation;
using AutoLot.Domain.Common;

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
            .NotEmpty().WithMessage(MessageCodes.ProfileDisplayNameRequired)
            .MinimumLength(2).WithMessage(MessageCodes.AuthDisplayNameTooShort)
            .MaximumLength(100).WithMessage(MessageCodes.AuthDisplayNameTooLong);

        // Порожній телефон дозволений: це спосіб його прибрати.
        RuleFor(request => request.PhoneNumber)
            .Matches(PhonePattern).WithMessage(MessageCodes.AuthPhoneFormat)
            .When(request => !string.IsNullOrWhiteSpace(request.PhoneNumber));
    }
}
