using AutoLot.Domain.Enums;

namespace AutoLot.Application.Listings.Dtos;

/// <summary>
/// Оголошення у вигляді, придатному для заповнення форми.
///
/// Навіщо окремий тип поруч із <see cref="ListingDetails"/>: той призначений
/// для показу й віддає НАЗВИ — «BMW», «X5», «Київ», «Підігрів сидінь». Формі
/// ж потрібні ІДЕНТИФІКАТОРИ, бо саме їх вона надішле назад. Спроба
/// відновити ідентифікатор із назви — це пошук по довіднику з передбачуваними
/// помилками на однакових назвах моделей у різних марок.
///
/// Розділення заодно тримає публічну картку чистою: ідентифікатори довідників
/// цікаві лише авторові оголошення, а картку бачить кожен відвідувач.
/// </summary>
public sealed record ListingDraft
{
    public long Id { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Область міста. У самому оголошенні її немає — місто знає свою область
    /// саме. Але формі вона потрібна: вибір міста двоступеневий, і без області
    /// не показати правильний список міст.
    /// </summary>
    public long RegionId { get; init; }

    public long CityId { get; init; }

    public long? CityDistrictId { get; init; }

    public decimal Price { get; init; }

    public Currency Currency { get; init; }

    public decimal? ReservePrice { get; init; }

    public ListingType Type { get; init; }

    public bool IsNegotiable { get; init; }

    public bool AcceptsTrade { get; init; }

    public bool IsUrgent { get; init; }

    /// <summary>Салон, від імені якого подано оголошення.</summary>
    public long? DealershipId { get; init; }

    /// <summary>
    /// Стан потрібен формі, щоб пояснити, ЧОМУ вона відкрилася: чернетку
    /// дописують, а відхилене виправляють — і в другому випадку доречно
    /// показати зауваження модератора.
    /// </summary>
    public ListingStatus Status { get; init; }

    public string? RejectionReason { get; init; }

    /// <summary>
    /// Той самий тип, який форма надішле назад. Спільний тип на обидва
    /// напрямки — найкоротший спосіб гарантувати, що нічого не загубиться:
    /// нове поле не можна додати лише в один бік.
    /// </summary>
    public CarSpecification Car { get; init; } = new();
}
