namespace AutoLot.Domain.Enums;

/// <summary>Ступінь пошкодження. «На запчастини» — авто, яке не поїде без ремонту.</summary>
public enum DamageState
{
    NotDamaged = 0,
    Damaged = 1,
    ForParts = 2,
}
