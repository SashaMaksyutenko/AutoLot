using FluentValidation;
using AutoLot.Application.Auth.Dtos;
using AutoLot.Application.Common.Localization;

namespace AutoLot.Application.Auth.Validation;

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(request => request.Email)
            .NotEmpty().WithMessage(MessageCodes.AuthEmailRequired)
            .MaximumLength(256).WithMessage(MessageCodes.AuthEmailTooLong);

        RuleFor(request => request.Password)
            .NotEmpty().WithMessage(MessageCodes.AuthPasswordRequired)
            .MaximumLength(128).WithMessage(MessageCodes.AuthPasswordTooLong);
    }
}
