using AutoLot.Domain.Enums;

namespace AutoLot.Application.Listings.Dtos;

/// <summary>Тіло скарги, яку надсилає відвідувач.</summary>
public sealed record SubmitReportRequest
{
    public ListingReportReason Reason { get; init; }

    /// <summary>Пояснення. Для причини «інше» обов'язкове — див. валідатор.</summary>
    public string? Comment { get; init; }
}

/// <summary>
/// Підтвердження, що скаргу прийнято. Навмисно бідне: скаржнику не показуємо
/// ні черги, ні чужих скарг на той самий лот — це робота модератора.
/// </summary>
public sealed record ReportReceipt(
    long Id,
    long ListingId,
    ListingReportReason Reason,
    DateTimeOffset CreatedAt,

    /// <summary>
    /// <c>false</c>, якщо така скарга від цієї людини вже була. Кнопка тоді
    /// каже «ви вже скаржилися», а не вдає, ніби прийняла ще одну.
    /// </summary>
    bool IsNew);

/// <summary>
/// Скарга в черзі модератора. Тут навпаки — усе, що потрібно для рішення,
/// щоб не довелося відкривати оголошення в сусідній вкладці.
/// </summary>
public sealed record ReportSummary(
    long Id,
    long ListingId,
    string ListingTitle,
    string? ListingPhoto,
    decimal ListingPrice,
    ListingReportReason Reason,

    /// <summary>Причина словами, уже перекладена мовою модератора.</summary>
    string ReasonName,
    string? Comment,
    string ReporterName,
    DateTimeOffset CreatedAt,

    /// <summary>
    /// Скільки ще скарг на це саме оголошення чекають розгляду. П'ять скарг
    /// за годину — це вже не суперечка смаків, і модератор має бачити це
    /// одразу, не гортаючи чергу.
    /// </summary>
    int OtherPendingForListing);

/// <summary>Рішення модератора.</summary>
public sealed record ResolveReportRequest
{
    /// <summary><c>true</c> — скарга слушна, оголошення знімаємо з публікації.</summary>
    public bool Accepted { get; init; }

    /// <summary>Нотатка для інших модераторів. Скаржник її не бачить.</summary>
    public string? Note { get; init; }
}
