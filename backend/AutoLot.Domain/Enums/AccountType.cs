namespace AutoLot.Domain.Enums;

/// <summary>Тип акаунта. Дилер отримує вітрину, бейдж і зняті ліміти (SPEC §3).</summary>
public enum AccountType
{
    Private = 0,
    Dealer = 1,
}
