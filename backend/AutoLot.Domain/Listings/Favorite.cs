using AutoLot.Domain.Identity;

namespace AutoLot.Domain.Listings;

/// <summary>
/// Позначка «це оголошення мене цікавить». Власного ключа не має — його роль
/// грає пара UserId + ListingId, і вона ж не дає додати те саме двічі:
/// база просто не прийме другий такий рядок.
///
/// Час потрібен, щоб показувати обране в порядку додавання — найсвіжіше
/// зверху. Поля «коли змінено» тут немає: позначку не редагують, її або
/// ставлять, або знімають.
/// </summary>
public sealed class Favorite
{
    public long UserId { get; set; }

    public User User { get; set; } = null!;

    public long ListingId { get; set; }

    public Listing Listing { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }
}
