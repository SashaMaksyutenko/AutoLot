using AutoLot.Application.Chat.Dtos;
using FluentValidation;

namespace AutoLot.Application.Chat.Validation;

public sealed class SendMessageRequestValidator : AbstractValidator<SendMessageRequest>
{
    public SendMessageRequestValidator()
    {
        RuleFor(request => request.Text)
            .NotEmpty().WithMessage("Повідомлення не може бути порожнім.")
            .MaximumLength(4000).WithMessage("Повідомлення задовге — до 4000 символів.");
    }
}
