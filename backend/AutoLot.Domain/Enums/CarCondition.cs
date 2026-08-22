namespace AutoLot.Domain.Enums;

/// <summary>
/// Новий чи вживаний. Для нового не мають сенсу пробіг і кількість власників —
/// за цим стежить валідація характеристик.
/// </summary>
public enum CarCondition
{
    Used = 0,
    New = 1,
}
