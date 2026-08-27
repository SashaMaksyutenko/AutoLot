namespace AutoLot.Application.Listings.Dtos;

/// <summary>
/// Питання під лотом разом із відповіддю, якщо вона є. Одна сутність назовні,
/// а не дві: на сторінці вони й показуються парою.
/// </summary>
public sealed record QuestionRecord(
    long Id,
    string AskerName,
    string Text,
    DateTimeOffset CreatedAt,
    string? Answer,
    DateTimeOffset? AnsweredAt);

/// <summary>Текст питання. Окремий тип, щоб працювала перевірка через FluentValidation.</summary>
public sealed record AskQuestionRequest
{
    public string Text { get; init; } = string.Empty;
}

/// <summary>Текст відповіді продавця.</summary>
public sealed record AnswerQuestionRequest
{
    public string Text { get; init; } = string.Empty;
}
