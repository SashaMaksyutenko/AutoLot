using FluentValidation;
using AutoLot.Application.Auth.Dtos;

namespace AutoLot.Application.Auth.Validation;

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(request => request.Email)
            .NotEmpty().WithMessage("Вкажіть email.")
            .MaximumLength(256).WithMessage("Email задовгий.");

        RuleFor(request => request.Password)
            .NotEmpty().WithMessage("Вкажіть пароль.")
            .MaximumLength(128).WithMessage("Пароль задовгий.");
    }
}
