namespace AutoLot.Application.Listings.Dtos;

/// <summary>
/// Той, кому продавець міг продати авто.
///
/// Список береться з листування про цей лот, а не з усіх користувачів
/// майданчика. Причина проста: вибирати покупця зі списку на десятки тисяч
/// людей неможливо, а той, хто купив, майже напевно спершу написав.
/// </summary>
public sealed record BuyerCandidate(
    long Id,
    string DisplayName,

    /// <summary>Коли востаннє листувалися — за цим список і впорядковано.</summary>
    DateTimeOffset LastMessageAt,

    /// <summary>
    /// Чи це переможець торгів. Для аукціонного лота він тут єдиний і
    /// обраний наперед: домовлятися про угоду з кимось іншим після торгів —
    /// це обійти саму суть торгів.
    /// </summary>
    bool IsAuctionWinner);

/// <summary>Тіло запиту «продано».</summary>
public sealed record MarkSoldRequest
{
    /// <summary>Порожнє означає «продано поза майданчиком».</summary>
    public long? BuyerId { get; init; }
}
