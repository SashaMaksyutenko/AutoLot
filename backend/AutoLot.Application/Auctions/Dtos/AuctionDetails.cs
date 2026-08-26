using AutoLot.Domain.Enums;

namespace AutoLot.Application.Auctions.Dtos;

/// <summary>
/// Стан торгів для показу. Двох речей тут немає навмисно й ніколи не буде:
/// суми резерву та чужої стелі автоставки. Перше — комерційна таємниця
/// продавця, друге зруйнувало б саму механіку автоставки (SPEC §4).
/// </summary>
public sealed record AuctionDetails(
    long ListingId,
    Currency Currency,
    decimal StartPrice,
    decimal CurrentPrice,

    /// <summary>Скільки треба поставити щонайменше — рахує сервер, не клієнт.</summary>
    decimal MinimumNextBid,

    /// <summary>Чинний крок ставки: клієнту зручно підказати кілька готових сум.</summary>
    decimal BidStep,
    int BidCount,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    AuctionStatus Status,

    /// <summary>
    /// Чи є в лота резерв узагалі. Його відсутність показуємо як перевагу —
    /// учасник знає, що торгується не намарно.
    /// </summary>
    bool HasReserve,

    /// <summary>Чи дотягнули торги до резерву. Саму суму не розкриваємо.</summary>
    bool IsReserveMet,

    /// <summary>Ім'я лідера. Історія торгів публічна, тож ховати його немає сенсу.</summary>
    string? LeaderName,

    /// <summary>Чи лідирує зараз той, хто дивиться.</summary>
    bool IsViewerLeading,

    /// <summary>
    /// Чи може той, хто дивиться, ставити. Продавець на власний лот — ні,
    /// гість — ні. Клієнт за цим малює кнопку або пояснення замість неї.
    /// </summary>
    bool CanViewerBid,

    /// <summary>Номер лідера — щоб глядач упізнав себе після живої розсилки.</summary>
    long? LeaderId,

    /// <summary>
    /// Час сервера в мить відповіді. Годинник на пристрої може відставати або
    /// поспішати на хвилини, а таймер до кінця торгів мусить показувати
    /// правду — тож клієнт малює його з поправкою на цю різницю (SPEC §5).
    /// </summary>
    DateTimeOffset ServerTime);
