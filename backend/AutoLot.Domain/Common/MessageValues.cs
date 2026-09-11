namespace AutoLot.Domain.Common;

/// <summary>
/// Значення для підстановки в текст повідомлення про помилку.
///
/// Навіщо це взагалі: частина повідомлень має число всередині — «до 20 фото»,
/// «ставка не менша за 25 400». Написати його прямо в тексті не можна, бо
/// число живе в налаштуваннях і змінюється, а перекладати текст щоразу
/// заново — тим паче.
///
/// Возимо значення у вбудованому <see cref="Exception.Data"/>. Це готове
/// сховище «додаткових відомостей про виняток», яке є в кожного винятку без
/// винятку — тож не довелося ні вводити спільний базовий клас для десятка
/// різних типів, ні змінювати їхні конструктори.
/// </summary>
public static class MessageValues
{
    /// <summary>
    /// Додає значення до винятку й повертає його ж — щоб писалося одним
    /// виразом: <c>throw new DomainRuleException(Code).With("limit", 20)</c>.
    /// </summary>
    public static TException With<TException>(this TException exception, string name, object value)
        where TException : Exception
    {
        ArgumentNullException.ThrowIfNull(exception);

        exception.Data[name] = value;

        return exception;
    }

    /// <summary>
    /// Читає значення назад. Порожній словник, а не <c>null</c>: тому, хто
    /// підставляє, байдуже, чи були значення взагалі.
    /// </summary>
    public static IReadOnlyDictionary<string, object?> ValuesOf(this Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (exception.Data.Count == 0)
        {
            return new Dictionary<string, object?>();
        }

        var values = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var key in exception.Data.Keys)
        {
            // Data — нетипізований словник, тож ключем теоретично може бути
            // будь-що. Нас цікавлять лише рядкові імена підстановок.
            if (key is string name)
            {
                values[name] = exception.Data[key];
            }
        }

        return values;
    }
}
