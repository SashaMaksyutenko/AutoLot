using AutoLot.Domain.Enums;

namespace AutoLot.Application.Auctions.Dtos;

/// <summary>
/// Підсумок торгів — те, що розсилається всім глядачам у мить закриття.
///
/// Переможця може й не бути: лот міг не зібрати жодної ставки або не
/// дотягнути до резерву. Тоді <see cref="WinnerId"/> порожній, а
/// <see cref="IsReserveMet"/> пояснює причину.
/// </summary>
public sealed record AuctionOutcome(
    long ListingId,
    decimal FinalPrice,
    Currency Currency,
    int BidCount,
    long? WinnerId,
    string? WinnerName,
    bool IsReserveMet,
    DateTimeOffset EndedAt,
    DateTimeOffset ServerTime);
