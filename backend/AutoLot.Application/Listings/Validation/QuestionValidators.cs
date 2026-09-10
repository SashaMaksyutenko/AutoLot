using AutoLot.Application.Listings.Dtos;
using FluentValidation;
using AutoLot.Application.Common.Localization;

namespace AutoLot.Application.Listings.Validation;

/// <summary>
/// Межі тексту питання. Верхня — щоб під лотом не з'явилося полотно на два
/// екрани; нижня — щоб «?» не вважалося питанням.
/// </summary>
public sealed class AskQuestionRequestValidator : AbstractValidator<AskQuestionRequest>
{
    public AskQuestionRequestValidator()
    {
        RuleFor(request => request.Text)
            .NotEmpty().WithMessage(MessageCodes.QuestionTextRequired)
            .MinimumLength(5).WithMessage(MessageCodes.QuestionTextTooShort)
            .MaximumLength(1000).WithMessage(MessageCodes.QuestionTextTooLong);
    }
}

public sealed class AnswerQuestionRequestValidator : AbstractValidator<AnswerQuestionRequest>
{
    public AnswerQuestionRequestValidator()
    {
        RuleFor(request => request.Text)
            .NotEmpty().WithMessage(MessageCodes.QuestionAnswerRequired)
            .MaximumLength(2000).WithMessage(MessageCodes.QuestionAnswerTooLong);
    }
}
