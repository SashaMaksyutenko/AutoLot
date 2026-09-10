using FluentValidation;
using AutoLot.Application.Auth.Dtos;
using AutoLot.Application.Common.Localization;

namespace AutoLot.Application.Auth.Validation;

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    /// <summary>Український мобільний у міжнародному форматі.</summary>
    private const string PhonePattern = @"^\+380\d{9}$";

    public RegisterRequestValidator()
    {
        RuleFor(request => request.Email)
            .NotEmpty().WithMessage(MessageCodes.AuthEmailRequired)
            .MaximumLength(256).WithMessage(MessageCodes.AuthEmailTooLong)
            .EmailAddress().WithMessage(MessageCodes.AuthEmailMalformed);

        // Політика має збігатися з IdentityOptions у AddIdentityCore,
        // інакше користувач побачить помилку вже після проходження валідації.
        RuleFor(request => request.Password)
            .NotEmpty().WithMessage(MessageCodes.AuthPasswordRequired)
            .MinimumLength(8).WithMessage(MessageCodes.AuthPasswordTooShort)
            .MaximumLength(128).WithMessage(MessageCodes.AuthPasswordTooLong)
            .Matches("[a-z]").WithMessage(MessageCodes.AuthPasswordNeedsLower)
            .Matches("[A-Z]").WithMessage(MessageCodes.AuthPasswordNeedsUpper)
            .Matches("[0-9]").WithMessage(MessageCodes.AuthPasswordNeedsDigit);

        RuleFor(request => request.DisplayName)
            .NotEmpty().WithMessage(MessageCodes.AuthDisplayNameRequired)
            .MinimumLength(2).WithMessage(MessageCodes.AuthDisplayNameTooShort)
            .MaximumLength(100).WithMessage(MessageCodes.AuthDisplayNameTooLong);

        RuleFor(request => request.AccountType)
            .IsInEnum().WithMessage(MessageCodes.AuthAccountTypeUnknown);

        RuleFor(request => request.PhoneNumber)
            .Matches(PhonePattern).WithMessage(MessageCodes.AuthPhoneFormat)
            .When(request => !string.IsNullOrWhiteSpace(request.PhoneNumber));
    }
}
