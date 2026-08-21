namespace AutoLot.Application.Users;

/// <summary>
/// Клієнт надіслав ідентифікатори, яких немає, або район, що належить іншому
/// місту. Це помилка запиту, а не збій, тож контролер перетворює її на 400.
/// </summary>
public sealed class InvalidLocationException(string message) : Exception(message);
