using AutoLot.Application.Chat.Dtos;
using FluentValidation;
using AutoLot.Domain.Common;

namespace AutoLot.Application.Chat.Validation;

public sealed class SendMessageRequestValidator : AbstractValidator<SendMessageRequest>
{
    public SendMessageRequestValidator()
    {
        RuleFor(request => request.Text)
            .NotEmpty().WithMessage(MessageCodes.ChatMessageRequired)
            .MaximumLength(4000).WithMessage(MessageCodes.ChatMessageTooLong);
    }
}
