using AutoLot.Domain.Common;
using AutoLot.Domain.Geo;
using AutoLot.Domain.Identity;

namespace AutoLot.Domain.Dealers;

/// <summary>
/// Автосалон. Окрема сутність, а не просто тип акаунта: у салоні працюють
/// люди, і оголошення подає хтось із них, а не «салон» абстрактно.
///
/// Головна причина саме такої моделі — звільнення менеджера. Якби оголошення
/// належали особисто йому, вони пішли б разом із ним. Належать салону —
/// лишаються, хоч би скільки разів змінився персонал.
/// </summary>
public sealed class Dealership : AuditableEntity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Частина адреси сторінки салону: /dealers/avto-plus.</summary>
    public string Slug { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? LogoPath { get; set; }

    public long CityId { get; set; }

    public City City { get; set; } = null!;

    /// <summary>
    /// Перевірений салон отримує бейдж. Без перевірки він працює так само —
    /// верифікація додає довіри, а не прав.
    /// </summary>
    public bool IsVerified { get; set; }

    public DateTimeOffset? VerifiedAt { get; set; }

    /// <summary>Хто саме перевірив — за SPEC §8 такі дії підлягають аудиту.</summary>
    public long? VerifiedById { get; set; }

    public User? VerifiedBy { get; set; }

    public ICollection<DealershipMember> Members { get; } = [];

    /// <summary>Ставить бейдж перевіреного. Повторний виклик лише оновлює, ким і коли.</summary>
    public void Verify(long moderatorId, DateTimeOffset now)
    {
        IsVerified = true;
        VerifiedAt = now;
        VerifiedById = moderatorId;
    }

    /// <summary>
    /// Знімає бейдж — наприклад, коли салон почав порушувати правила.
    /// Слід про те, хто востаннє перевіряв, лишаємо навмисно: історія рішень
    /// цінніша за чистоту полів.
    /// </summary>
    public void RevokeVerification()
    {
        IsVerified = false;
    }
}
