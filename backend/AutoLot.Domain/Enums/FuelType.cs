namespace AutoLot.Domain.Enums;

/// <summary>
/// Тип пального. Гібрид і плагін-гібрид розділені навмисно: перший заряджається
/// лише від двигуна й гальмування, другий — ще й від розетки, і покупці шукають
/// саме другий.
/// </summary>
public enum FuelType
{
    Petrol = 0,
    Diesel = 1,
    Gas = 2,
    PetrolGas = 3,
    Hybrid = 4,
    PluginHybrid = 5,
    Electric = 6,
    Hydrogen = 7,
}
