namespace AutoLot.Application.Listings.Dtos;

/// <summary>Тіло відгуку. Про кого він — сервер виводить сам зі складу угоди.</summary>
public sealed record LeaveReviewRequest
{
    public int Rating { get; init; }

    public string? Text { get; init; }
}

/// <summary>Відгук для показу.</summary>
public sealed record ReviewRecord(
    long Id,
    long ListingId,
    string ListingTitle,
    long AuthorId,
    string AuthorName,
    long SubjectId,
    int Rating,
    string? Text,
    DateTimeOffset CreatedAt,

    /// <summary>Хто написав. За цим підпис «відгук продавця» чи «відгук покупця».</summary>
    bool AuthorIsSeller);

/// <summary>
/// Рейтинг людини одним рядком.
///
/// Середнє показуємо разом із кількістю навмисно: «5,0» з одного відгуку й
/// «4,7» із сорока — це різні речі, а сама цифра їх не розрізняє.
/// </summary>
public sealed record RatingSummary(int Count, decimal Average);

/// <summary>
/// Стан відгуків під однією угодою — усе, що потрібно сторінці авто, щоб
/// вирішити, показувати форму чи вже написане.
/// </summary>
public sealed record DealReviews(
    /// <summary>Чи має той, хто дивиться, право написати відгук саме зараз.</summary>
    bool CanReview,

    /// <summary>
    /// Усі відгуки про цю угоду — і власний теж.
    ///
    /// Саме список, а не пара «мій / чужий»: гість не має «свого», і в такій
    /// парі йому не лишалося б нічого. А він і є той, заради кого відгуки
    /// публічні — покупець дивиться репутацію ДО того, як написати.
    /// </summary>
    IReadOnlyList<ReviewRecord> Reviews,

    /// <summary>
    /// Ідентифікатор власного відгуку, якщо він є. Клієнту лишається
    /// підписати його «ваш», не звіряючи авторів самотужки.
    /// </summary>
    long? MineId);
