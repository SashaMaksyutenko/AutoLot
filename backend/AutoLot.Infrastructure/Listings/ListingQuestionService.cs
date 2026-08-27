using AutoLot.Application.Common.Abstractions;
using AutoLot.Application.Listings;
using AutoLot.Application.Listings.Dtos;
using AutoLot.Domain.Enums;
using AutoLot.Domain.Listings;
using AutoLot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AutoLot.Infrastructure.Listings;

/// <summary>
/// Публічні питання під оголошенням. Хто питає й хто відповідає — вирішується
/// тут, за даними з бази, а не за тим, що надіслав клієнт (SPEC §8).
/// </summary>
internal sealed class ListingQuestionService(
    AutoLotDbContext dbContext,
    IDateTimeProvider clock) : IListingQuestionService
{
    /// <summary>Стани, у яких оголошення видно всім, — ті самі, що й у каталозі.</summary>
    private static readonly ListingStatus[] PubliclyVisible =
        [ListingStatus.Active, ListingStatus.Sold];

    public async Task<IReadOnlyList<QuestionRecord>> GetAsync(
        long listingId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.ListingQuestions
            .AsNoTracking()
            .Where(question => question.ListingId == listingId)
            .OrderByDescending(question => question.CreatedAt)
            .ThenByDescending(question => question.Id)
            .Select(question => new QuestionRecord(
                question.Id,
                question.Asker.DisplayName,
                question.Text,
                question.CreatedAt,
                question.Answer,
                question.AnsweredAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<QuestionRecord> AskAsync(
        long listingId,
        long askerId,
        string text,
        CancellationToken cancellationToken = default)
    {
        var listing = await dbContext.Listings
            .AsNoTracking()
            .Where(item => item.Id == listingId)
            .Select(item => new { item.Id, item.SellerId, item.Status })
            .FirstOrDefaultAsync(cancellationToken);

        if (listing is null || !PubliclyVisible.Contains(listing.Status))
        {
            throw new ListingNotFoundException(listingId);
        }

        if (listing.SellerId == askerId)
        {
            throw new ListingAccessException("Питати самого себе не можна — відповідайте на чужі.");
        }

        var question = new ListingQuestion
        {
            ListingId = listingId,
            AskerId = askerId,
            Text = text.Trim(),
            CreatedAt = clock.UtcNow,
        };

        dbContext.ListingQuestions.Add(question);
        await dbContext.SaveChangesAsync(cancellationToken);

        var askerName = await GetDisplayNameAsync(askerId, cancellationToken);

        return ToRecord(question, askerName);
    }

    public async Task<QuestionRecord> AnswerAsync(
        long questionId,
        long sellerId,
        string text,
        CancellationToken cancellationToken = default)
    {
        var question = await dbContext.ListingQuestions
            .Include(item => item.Listing)
            .FirstOrDefaultAsync(item => item.Id == questionId, cancellationToken)
            ?? throw new QuestionNotFoundException(questionId);

        // Відповідати може лише продавець. Це саме 403, а не 404: питання
        // публічне, його існування ні для кого не таємниця.
        if (question.Listing.SellerId != sellerId)
        {
            throw new ListingAccessException("Відповідати на питання може лише продавець.");
        }

        question.Reply(text, clock.UtcNow);

        await dbContext.SaveChangesAsync(cancellationToken);

        var askerName = await GetDisplayNameAsync(question.AskerId, cancellationToken);

        return ToRecord(question, askerName);
    }

    private async Task<string> GetDisplayNameAsync(long userId, CancellationToken cancellationToken)
    {
        return await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => user.DisplayName)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;
    }

    private static QuestionRecord ToRecord(ListingQuestion question, string askerName)
    {
        return new QuestionRecord(
            question.Id,
            askerName,
            question.Text,
            question.CreatedAt,
            question.Answer,
            question.AnsweredAt);
    }
}
