using AutoLot.Domain.Common;
using AutoLot.Domain.Identity;
using AutoLot.Domain.Listings;

namespace AutoLot.Domain.Auctions;

/// <summary>
/// Коментар під лотом, написаний під час торгів.
///
/// Чим це відрізняється від питань продавцю (<see cref="ListingQuestion"/>).
/// Питання — це розмова «покупець → продавець»: у нього одна відповідь, і та
/// лише від продавця. Коментарі — розмова всіх з усіма й у реальному часі:
/// «на третьому фото видно іржу на порозі», «такий мотор ходить 400 тисяч»,
/// «ціна вже вища за ринок». Саме через них торги на Cars & Bids живі, а не
/// перелік цифр, і саме тому вони йдуть окремо: змішані з питаннями, вони
/// поховали б відповіді продавця під обговоренням.
///
/// Коментар не редагують і не видаляють: у живій розмові правка заднім числом
/// перекручує все, що написали у відповідь.
/// </summary>
public sealed class AuctionComment : Entity
{
    /// <summary>Досить на кілька речень, замало на простирадло.</summary>
    public const int MaxLength = 1000;

    public long ListingId { get; set; }

    public Listing Listing { get; set; } = null!;

    public long AuthorId { get; set; }

    public User Author { get; set; } = null!;

    public string Text { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}
