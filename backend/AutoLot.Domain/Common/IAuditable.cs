namespace AutoLot.Domain.Common;

/// <summary>
/// Сутність, для якої інфраструктура сама проставляє час створення та зміни.
/// Окремий інтерфейс, а не лише базовий клас, бо частина сутностей успадковує
/// чужу ієрархію (наприклад, користувач — від IdentityUser).
/// </summary>
public interface IAuditable
{
    DateTimeOffset CreatedAt { get; set; }

    DateTimeOffset? UpdatedAt { get; set; }
}
