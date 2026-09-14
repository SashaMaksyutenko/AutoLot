using AutoLot.Domain.Common;
using AutoLot.Domain.Identity;

namespace AutoLot.Domain.Listings;

/// <summary>
/// Що людина дивилася й коли востаннє.
///
/// Один рядок на пару «людина — оголошення», а не на кожен перегляд. Історія
/// відповідає на питання «що я нещодавно дивився», і десять заходів на те
/// саме авто — це один пункт у ній, а не десять. Заразом таблиця росте від
/// РІЗНИХ авто, а не від кількості кліків.
/// </summary>
public sealed class ListingView : Entity
{
    /// <summary>
    /// Скільки пунктів історії тримаємо на людину. Далі найдавніші зникають:
    /// список, у якому щось із позаминулого року, ніхто не гортає, а рядки в
    /// базі лишалися б назавжди.
    /// </summary>
    public const int PerUserLimit = 100;

    public long UserId { get; set; }

    public User User { get; set; } = null!;

    public long ListingId { get; set; }

    public Listing Listing { get; set; } = null!;

    public DateTimeOffset ViewedAt { get; set; }
}
