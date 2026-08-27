using AutoLot.Application.Listings.Dtos;
using FluentValidation;

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
            .NotEmpty().WithMessage("Питання не може бути порожнім.")
            .MinimumLength(5).WithMessage("Питання надто коротке.")
            .MaximumLength(1000).WithMessage("Питання задовге — до 1000 символів.");
    }
}

public sealed class AnswerQuestionRequestValidator : AbstractValidator<AnswerQuestionRequest>
{
    public AnswerQuestionRequestValidator()
    {
        RuleFor(request => request.Text)
            .NotEmpty().WithMessage("Відповідь не може бути порожньою.")
            .MaximumLength(2000).WithMessage("Відповідь задовга — до 2000 символів.");
    }
}
