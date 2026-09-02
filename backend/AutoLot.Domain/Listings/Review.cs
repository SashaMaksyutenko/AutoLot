using AutoLot.Domain.Common;
using AutoLot.Domain.Identity;

namespace AutoLot.Domain.Listings;

/// <summary>
/// Взаємний відгук після угоди (SPEC §4).
///
/// Прив'язаний до ОГОЛОШЕННЯ, а не просто до пари людей. Це і є те, що
/// відрізняє відгук від відгуку: за ним стоїть конкретна продана машина,
/// а не абстрактна думка про людину. Без такої прив'язки будь-хто міг би
/// написати будь-кому, і рейтинг вартував би рівно нічого.
///
/// Відгук **публічний і незмінний**. Публічний — бо в цьому вся його
/// користь; незмінний — бо відгук, який можна переписати після сварки,
/// перестає бути свідченням про угоду. Виправляти помилкові має модератор,
/// видаляючи, а не автор, переписуючи.
/// </summary>
public sealed class Review : Entity
{
    /// <summary>Нижня межа оцінки.</summary>
    public const int MinRating = 1;

    /// <summary>
    /// Верхня межа. П'ять, а не десять: на десятибальній шкалі люди все одно
    /// ставлять або 10, або 1, і проміжні значення нічого не додають.
    /// </summary>
    public const int MaxRating = 5;

    public long ListingId { get; set; }

    public Listing Listing { get; set; } = null!;

    /// <summary>Хто пише — одна зі сторін угоди.</summary>
    public long AuthorId { get; set; }

    public User Author { get; set; } = null!;

    /// <summary>
    /// Про кого. Не вибирається, а виводиться: у кожної угоди рівно дві
    /// сторони, тож якщо пише покупець — відгук про продавця, і навпаки.
    /// </summary>
    public long SubjectId { get; set; }

    public User Subject { get; set; } = null!;

    public int Rating { get; set; }

    /// <summary>
    /// Текст. Не обов'язковий: сама оцінка вже щось каже, а вимога писати
    /// відлякує половину тих, хто поставив би зірки.
    /// </summary>
    public string? Text { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Чи писав це продавець. Потрібне, щоб показати «відгук покупця» / «відгук продавця».</summary>
    public bool AuthorIsSeller { get; set; }

    /// <summary>
    /// Створює відгук, перевіривши те, що можна перевірити без бази.
    /// </summary>
    public static Review Create(
        long listingId,
        long authorId,
        long subjectId,
        bool authorIsSeller,
        int rating,
        string? text,
        DateTimeOffset now)
    {
        if (rating is < MinRating or > MaxRating)
        {
            throw new DomainRuleException($"Оцінка має бути від {MinRating} до {MaxRating}.");
        }

        // Сам собі відгук не пишуть. Дійти сюди можна лише помилкою в коді,
        // але правило зберігається поруч із рештою — там, де його шукатимуть.
        if (authorId == subjectId)
        {
            throw new DomainRuleException("Відгук самому собі не має сенсу.");
        }

        var trimmed = text?.Trim();

        return new Review
        {
            ListingId = listingId,
            AuthorId = authorId,
            SubjectId = subjectId,
            AuthorIsSeller = authorIsSeller,
            Rating = rating,
            Text = string.IsNullOrEmpty(trimmed) ? null : trimmed,
            CreatedAt = now,
        };
    }
}
