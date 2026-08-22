namespace AutoLot.Application.Cars.Dtos;

/// <summary>
/// Значення довідника-перелічення. <paramref name="Value"/> — це те, що
/// клієнт надішле назад ("Sedan"), <paramref name="Name"/> — те, що бачить
/// людина ("Седан").
/// </summary>
public sealed record LookupItem(string Value, string Name);
