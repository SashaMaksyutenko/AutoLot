using AutoLot.Domain.Common;
using AutoLot.Domain.Enums;
using AutoLot.Domain.Identity;

namespace AutoLot.Domain.Listings;

/// <summary>
/// Скарга на оголошення (SPEC §8).
///
/// Скарга — це не думка про авто, а сигнал модератору: «тут порушення».
/// Тому вона **непублічна**: ні автор оголошення, ні інші відвідувачі її не
/// бачать. Інакше скарга стала б знаряддям тиску — конкурент писав би її, щоб
/// зіпсувати вигляд чужого лота, а не щоб хтось розібрався.
///
/// Хто поскаржився, знає лише модератор. Автор оголошення бачить наслідок
/// («знято з публікації, причина така-то»), але не ім'я — інакше скарги
/// перетворилися б на привід для помсти й ніхто б їх не писав.
/// </summary>
public sealed class ListingReport : Entity
{
    public long ListingId { get; set; }

    public Listing Listing { get; set; } = null!;

    /// <summary>Хто поскаржився. Анонімних скарг не буває: за них треба відповідати.</summary>
    public long ReporterId { get; set; }

    public User Reporter { get; set; } = null!;

    public ListingReportReason Reason { get; set; }

    /// <summary>Пояснення своїми словами. Обов'язкове лише для причини «інше».</summary>
    public string? Comment { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public ListingReportStatus Status { get; set; } = ListingReportStatus.Pending;

    /// <summary>Хто розглянув. Порожнє, поки скарга в черзі.</summary>
    public long? ReviewedById { get; set; }

    public User? ReviewedBy { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }

    /// <summary>Нотатка модератора для інших модераторів, не для скаржника.</summary>
    public string? ReviewNote { get; set; }

    public bool IsPending => Status == ListingReportStatus.Pending;

    /// <summary>
    /// Модератор виносить рішення.
    /// </summary>
    /// <param name="accepted">
    /// <c>true</c> — скарга слушна; <c>false</c> — порушення немає.
    /// </param>
    /// <remarks>
    /// Повторний розгляд заборонено: рішення вже потягло за собою дію з
    /// оголошенням, і «перерозгляд» мовчки розійшовся б із тим, що вже
    /// сталося. Якщо модератор помилився, оголошення повертають через його
    /// власний цикл — автор виправляє й подає знову.
    /// </remarks>
    public void Resolve(bool accepted, long moderatorId, DateTimeOffset now, string? note)
    {
        if (!IsPending)
        {
            throw new DomainRuleException("Скаргу вже розглянуто.");
        }

        Status = accepted ? ListingReportStatus.Accepted : ListingReportStatus.Dismissed;
        ReviewedById = moderatorId;
        ReviewedAt = now;

        // Порожню нотатку зводимо до null, щоб у базі не було рядків із
        // самих пробілів: перевірка «нотатка є» стала б брехливою.
        var trimmed = note?.Trim();
        ReviewNote = string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
