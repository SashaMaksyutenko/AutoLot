using AutoLot.Application.Listings.Dtos;

namespace AutoLot.Application.Listings;

/// <summary>
/// Публічні питання під оголошенням (SPEC §4).
///
/// Питати може будь-який автентифікований користувач, крім самого продавця —
/// розмовляти із собою немає сенсу. Відповідати може лише продавець.
/// </summary>
public interface IListingQuestionService
{
    /// <summary>Усі питання лота, найновіші зверху. Доступно всім, і гостям теж.</summary>
    Task<IReadOnlyList<QuestionRecord>> GetAsync(
        long listingId,
        CancellationToken cancellationToken = default);

    Task<QuestionRecord> AskAsync(
        long listingId,
        long askerId,
        string text,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Відповідь продавця. Повторний виклик переписує попередню — виправити
    /// щойно написане має бути можна.
    /// </summary>
    Task<QuestionRecord> AnswerAsync(
        long questionId,
        long sellerId,
        string text,
        CancellationToken cancellationToken = default);
}

/// <summary>Питання немає або воно під недоступним оголошенням.</summary>
public sealed class QuestionNotFoundException(long questionId)
    : Exception($"Питання {questionId} не знайдено.");
