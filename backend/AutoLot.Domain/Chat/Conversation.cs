using AutoLot.Domain.Common;
using AutoLot.Domain.Identity;
using AutoLot.Domain.Listings;

namespace AutoLot.Domain.Chat;

/// <summary>
/// Приватна розмова про одне оголошення.
///
/// Це НЕ те саме, що публічні питання під лотом. Там відповідь бачить кожен,
/// бо вона стосується самого авто; тут — домовленості про час огляду, торг,
/// адресу. Плутати їх не можна: питання «чи фарбоване крило» має бути
/// публічним, а «буду о шостій біля метро» — ні.
///
/// Розмова завжди прив'язана до оголошення: без нього незрозуміло, про що
/// мова, і продавець з десятком авто не зрозуміє, яке саме цікавить.
/// </summary>
public sealed class Conversation : Entity
{
    public long ListingId { get; set; }

    public Listing Listing { get; set; } = null!;

    /// <summary>Хто почав розмову. Продавець власну розмову почати не може.</summary>
    public long BuyerId { get; set; }

    public User Buyer { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Час останнього повідомлення. Зберігається окремо, хоч і виводиться зі
    /// списку: за ним сортується перелік розмов, і рахувати його щоразу
    /// підзапитом означало б платити за кожен рядок списку.
    /// </summary>
    public DateTimeOffset LastMessageAt { get; set; }

    public ICollection<Message> Messages { get; } = [];
}
