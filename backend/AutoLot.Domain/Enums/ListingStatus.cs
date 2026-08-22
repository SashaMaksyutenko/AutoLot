namespace AutoLot.Domain.Enums;

/// <summary>
/// Життєвий цикл оголошення (SPEC §3):
/// Draft → PendingModeration → Active → (Sold | Expired | Rejected | Archived).
/// </summary>
public enum ListingStatus
{
    /// <summary>Автор ще редагує; бачить лише він.</summary>
    Draft = 0,

    /// <summary>Подано на модерацію, чекає рішення.</summary>
    PendingModeration = 1,

    /// <summary>Опубліковано й видно всім.</summary>
    Active = 2,

    Sold = 3,
    Expired = 4,

    /// <summary>Модератор відхилив; автор може виправити й подати знову.</summary>
    Rejected = 5,

    /// <summary>Прибрано автором із видачі, але не видалено.</summary>
    Archived = 6,
}
