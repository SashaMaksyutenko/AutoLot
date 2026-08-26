using AutoLot.Domain.Enums;

namespace AutoLot.Application.Auctions.Dtos;

/// <summary>
/// Те, що змінилося після ставки, — для всіх, хто дивиться лот.
///
/// Це НЕ <see cref="AuctionDetails"/>, і різниця принципова: у деталях є поля
/// «чи лідирую я» та «чи можу я ставити», а вони в кожного глядача свої.
/// Розсилка ж одна на всіх, тож тут лише те, що однакове для будь-кого.
/// Особисте клієнт добудовує сам, звіряючи <see cref="LeaderId"/> зі своїм.
/// </summary>
public sealed record AuctionUpdate(
    long ListingId,
    decimal CurrentPrice,
    decimal MinimumNextBid,
    decimal BidStep,
    int BidCount,

    /// <summary>Могло щойно відсунутися антиснайпінгом — таймер треба перемалювати.</summary>
    DateTimeOffset EndsAt,
    AuctionStatus Status,
    bool IsReserveMet,
    long? LeaderId,
    string? LeaderName,

    /// <summary>
    /// Нові рядки історії, найновіший першим. Їх може бути два: чужа
    /// автоставка відбивається тим самим рухом.
    /// </summary>
    IReadOnlyList<BidRecord> NewBids,

    /// <summary>
    /// Час сервера в момент розсилки. Годинник клієнта може відставати або
    /// поспішати на хвилини, а таймер до кінця торгів мусить показувати
    /// правду — тож він малюється з поправкою на цю різницю (SPEC §5).
    /// </summary>
    DateTimeOffset ServerTime);
