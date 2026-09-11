using AutoLot.Application.Auth.Dtos;
using FluentValidation;
using AutoLot.Domain.Common;

namespace AutoLot.Application.Auth.Validation;

public sealed class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator()
    {
        RuleFor(request => request.Email)
            .NotEmpty().WithMessage(MessageCodes.RecoveryEmailRequired)
            .EmailAddress().WithMessage(MessageCodes.RecoveryEmailMalformed)
            .MaximumLength(256).WithMessage(MessageCodes.RecoveryEmailTooLong);
    }
}

public sealed class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(request => request.Email)
            .NotEmpty().WithMessage(MessageCodes.RecoveryEmailRequired)
            .EmailAddress().WithMessage(MessageCodes.RecoveryEmailMalformed);

        RuleFor(request => request.Token)
            .NotEmpty().WithMessage(MessageCodes.RecoveryTokenIncomplete);

        // Ті самі правила, що при реєстрації. Дублюються свідомо: вимоги до
        // пароля не мусять залежати від того, яким шляхом його задають, і
        // спільний валідатор тут лише сховав би цю умову.
        RuleFor(request => request.NewPassword)
            .NotEmpty().WithMessage(MessageCodes.RecoveryPasswordRequired)
            .MinimumLength(8).WithMessage(MessageCodes.AuthPasswordTooShort)
            .MaximumLength(128).WithMessage(MessageCodes.AuthPasswordTooLong)
            .Matches("[a-z]").WithMessage(MessageCodes.AuthPasswordNeedsLower)
            .Matches("[A-Z]").WithMessage(MessageCodes.AuthPasswordNeedsUpper)
            .Matches("[0-9]").WithMessage(MessageCodes.AuthPasswordNeedsDigit);
    }
}

public sealed class ConfirmEmailRequestValidator : AbstractValidator<ConfirmEmailRequest>
{
    public ConfirmEmailRequestValidator()
    {
        RuleFor(request => request.Email)
            .NotEmpty().WithMessage(MessageCodes.RecoveryEmailRequired)
            .EmailAddress().WithMessage(MessageCodes.RecoveryEmailMalformed);

        RuleFor(request => request.Token)
            .NotEmpty().WithMessage(MessageCodes.RecoveryTokenIncomplete);
    }
}
