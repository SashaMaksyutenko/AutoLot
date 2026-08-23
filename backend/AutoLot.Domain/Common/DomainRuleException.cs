namespace AutoLot.Domain.Common;

/// <summary>
/// Порушено правило домену: спроба зробити те, чого сутність не дозволяє —
/// наприклад, опублікувати вже продане оголошення. Це помилка того, хто
/// викликав, а не збій, тож шар API перетворює її на 409 Conflict.
/// </summary>
public sealed class DomainRuleException(string message) : Exception(message);
