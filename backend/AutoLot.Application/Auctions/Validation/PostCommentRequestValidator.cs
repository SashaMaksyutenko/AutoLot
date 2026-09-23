using AutoLot.Application.Auctions.Dtos;
using AutoLot.Domain.Auctions;
using AutoLot.Domain.Common;
using FluentValidation;

namespace AutoLot.Application.Auctions.Validation;

/// <summary>
/// Межі тексту коментаря. Нижньої межі, на відміну від питань, немає:
/// «+1» чи «гарна машина» — цілком нормальний коментар у живій розмові.
/// </summary>
public sealed class PostCommentRequestValidator : AbstractValidator<PostCommentRequest>
{
    public PostCommentRequestValidator()
    {
        RuleFor(request => request.Text)
            .NotEmpty().WithMessage(MessageCodes.CommentTextRequired)
            .MaximumLength(AuctionComment.MaxLength).WithMessage(MessageCodes.CommentTextTooLong);
    }
}
