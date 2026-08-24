namespace AutoLot.Application.Common.Abstractions;

/// <summary>
/// Сховище файлів зображень. Окремий інтерфейс, бо локальна тека — це лише
/// сьогоднішній варіант: перехід на об'єктне сховище має бути заміною
/// реалізації, а не переписуванням сервісу оголошень.
/// </summary>
public interface IPhotoStorage
{
    /// <summary>
    /// Зберігає файл і повертає шлях відносно кореня сховища — саме він
    /// потрапляє в базу. Абсолютні шляхи туди класти не можна: вони зламаються
    /// на іншій машині.
    /// </summary>
    Task<string> SaveAsync(
        string relativeDirectory,
        string fileName,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken = default);

    /// <summary>Видаляє файл. Відсутній файл — не помилка.</summary>
    Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default);
}
