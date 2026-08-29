using AutoLot.Application.Auth.Dtos;
using FluentValidation;

namespace AutoLot.Application.Auth.Validation;

public sealed class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator()
    {
        RuleFor(request => request.Email)
            .NotEmpty().WithMessage("Вкажіть пошту.")
            .EmailAddress().WithMessage("Пошта виглядає некоректною.")
            .MaximumLength(256).WithMessage("Адреса задовга.");
    }
}

public sealed class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(request => request.Email)
            .NotEmpty().WithMessage("Вкажіть пошту.")
            .EmailAddress().WithMessage("Пошта виглядає некоректною.");

        RuleFor(request => request.Token)
            .NotEmpty().WithMessage("Посилання неповне — скористайтеся тим, що в листі.");

        // Ті самі правила, що при реєстрації. Дублюються свідомо: вимоги до
        // пароля не мусять залежати від того, яким шляхом його задають, і
        // спільний валідатор тут лише сховав би цю умову.
        RuleFor(request => request.NewPassword)
            .NotEmpty().WithMessage("Вкажіть новий пароль.")
            .MinimumLength(8).WithMessage("Пароль має містити щонайменше 8 символів.")
            .MaximumLength(128).WithMessage("Пароль задовгий.")
            .Matches("[a-z]").WithMessage("Пароль має містити малу літеру.")
            .Matches("[A-Z]").WithMessage("Пароль має містити велику літеру.")
            .Matches("[0-9]").WithMessage("Пароль має містити цифру.");
    }
}

public sealed class ConfirmEmailRequestValidator : AbstractValidator<ConfirmEmailRequest>
{
    public ConfirmEmailRequestValidator()
    {
        RuleFor(request => request.Email)
            .NotEmpty().WithMessage("Вкажіть пошту.")
            .EmailAddress().WithMessage("Пошта виглядає некоректною.");

        RuleFor(request => request.Token)
            .NotEmpty().WithMessage("Посилання неповне — скористайтеся тим, що в листі.");
    }
}
