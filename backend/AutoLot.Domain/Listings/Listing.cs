using AutoLot.Domain.Common;
using AutoLot.Domain.Enums;
using AutoLot.Domain.Geo;
using AutoLot.Domain.Identity;

namespace AutoLot.Domain.Listings;

/// <summary>
/// Оголошення про продаж. Саме воно проходить модерацію, має ціну й статус;
/// технічні характеристики винесені в <see cref="Car"/>.
/// </summary>
public sealed class Listing : AuditableEntity
{
    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public long SellerId { get; set; }

    public User Seller { get; set; } = null!;

    // ── Де продають ──────────────────────────────────────────────────

    public long CityId { get; set; }

    public City City { get; set; } = null!;

    public long? CityDistrictId { get; set; }

    public CityDistrict? CityDistrict { get; set; }

    // ── Ціна ─────────────────────────────────────────────────────────

    public decimal Price { get; set; }

    public Currency Currency { get; set; }

    /// <summary>
    /// Ціна, перерахована в гривню за курсом НБУ (SPEC §7). Потрібна, щоб
    /// сортувати й фільтрувати оголошення в різних валютах разом; сама ціна
    /// при цьому зберігається такою, як її ввів продавець.
    /// </summary>
    public decimal PriceUah { get; set; }

    // ── Умови угоди ──────────────────────────────────────────────────

    public bool IsNegotiable { get; set; }

    public bool AcceptsTrade { get; set; }

    public bool IsUrgent { get; set; }

    // ── Стан оголошення ──────────────────────────────────────────────

    public ListingType Type { get; set; } = ListingType.FixedPrice;

    public ListingStatus Status { get; set; } = ListingStatus.Draft;

    /// <summary>Коли оголошення вперше стало видимим. Порожнє в чернетки.</summary>
    public DateTimeOffset? PublishedAt { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary>Причина відмови модератора — автор має розуміти, що виправляти.</summary>
    public string? RejectionReason { get; set; }

    /// <summary>Скільки разів картку відкривали. Оновлюється окремо від решти.</summary>
    public int ViewCount { get; set; }

    public Car Car { get; set; } = null!;
}
