using FluentValidation;
using AutoLot.Application.Auth.Dtos;

namespace AutoLot.Application.Auth.Validation;

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    /// <summary>Український мобільний у міжнародному форматі.</summary>
    private const string PhonePattern = @"^\+380\d{9}$";

    public RegisterRequestValidator()
    {
        RuleFor(request => request.Email)
            .NotEmpty().WithMessage("Вкажіть email.")
            .MaximumLength(256).WithMessage("Email задовгий.")
            .EmailAddress().WithMessage("Email виглядає некоректним.");

        // Політика має збігатися з IdentityOptions у AddIdentityCore,
        // інакше користувач побачить помилку вже після проходження валідації.
        RuleFor(request => request.Password)
            .NotEmpty().WithMessage("Вкажіть пароль.")
            .MinimumLength(8).WithMessage("Пароль має містити щонайменше 8 символів.")
            .MaximumLength(128).WithMessage("Пароль задовгий.")
            .Matches("[a-z]").WithMessage("Пароль має містити малу літеру.")
            .Matches("[A-Z]").WithMessage("Пароль має містити велику літеру.")
            .Matches("[0-9]").WithMessage("Пароль має містити цифру.");

        RuleFor(request => request.DisplayName)
            .NotEmpty().WithMessage("Вкажіть ім'я або назву салону.")
            .MinimumLength(2).WithMessage("Ім'я закоротке.")
            .MaximumLength(100).WithMessage("Ім'я задовге.");

        RuleFor(request => request.AccountType)
            .IsInEnum().WithMessage("Невідомий тип акаунта.");

        RuleFor(request => request.PhoneNumber)
            .Matches(PhonePattern).WithMessage("Телефон має бути у форматі +380XXXXXXXXX.")
            .When(request => !string.IsNullOrWhiteSpace(request.PhoneNumber));
    }
}
