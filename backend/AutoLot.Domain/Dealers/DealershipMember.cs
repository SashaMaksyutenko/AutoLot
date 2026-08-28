using AutoLot.Domain.Identity;

namespace AutoLot.Domain.Dealers;

/// <summary>
/// Хто працює в салоні. Власного ключа немає — його роль грає пара
/// «салон + користувач», і вона ж не дає додати ту саму людину двічі.
/// </summary>
public sealed class DealershipMember
{
    public long DealershipId { get; set; }

    public Dealership Dealership { get; set; } = null!;

    public long UserId { get; set; }

    public User User { get; set; } = null!;

    public DealershipRole Role { get; set; } = DealershipRole.Manager;

    public DateTimeOffset JoinedAt { get; set; }

    /// <summary>Власник — єдиний, хто керує складом персоналу.</summary>
    public bool CanManageStaff => Role == DealershipRole.Owner;
}

/// <summary>
/// Роль у салоні. Різниця між ними одна, зате суттєва: менеджер веде
/// оголошення, власник ще й вирішує, хто взагалі працює в салоні.
///
/// Це НЕ ролі майданчика (`User` / `Moderator` / `Admin`) — ті кажуть, що
/// людина може робити на сайті загалом, а ці діють у межах одного салону.
/// </summary>
public enum DealershipRole
{
    Manager = 0,
    Owner = 1,
}
