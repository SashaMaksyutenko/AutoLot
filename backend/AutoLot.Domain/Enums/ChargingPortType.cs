namespace AutoLot.Domain.Enums;

/// <summary>
/// Тип зарядного роз'єму електромобіля. Має сенс лише там, де є батарея,
/// тому поле необов'язкове.
/// </summary>
public enum ChargingPortType
{
    Type1 = 0,
    Type2 = 1,
    Ccs = 2,
    ChaDeMo = 3,
    Gbt = 4,
    Tesla = 5,
}
