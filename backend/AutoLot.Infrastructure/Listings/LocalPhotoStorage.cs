using AutoLot.Application.Common.Abstractions;
using Microsoft.Extensions.Options;

namespace AutoLot.Infrastructure.Listings;

/// <summary>
/// Зберігає зображення у локальній теці. Шлях у базі завжди відносний і
/// з прямими скісними — так він однаково працює і як URL, і на іншій ОС.
/// </summary>
internal sealed class LocalPhotoStorage(IOptions<PhotoStorageOptions> options) : IPhotoStorage
{
    private readonly string root = Path.GetFullPath(options.Value.RootPath);

    public async Task<string> SaveAsync(
        string relativeDirectory,
        string fileName,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken = default)
    {
        var relativePath = $"{relativeDirectory}/{fileName}";
        var fullPath = ResolveInsideRoot(relativePath);

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await File.WriteAllBytesAsync(fullPath, content, cancellationToken);

        return relativePath;
    }

    public Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var fullPath = ResolveInsideRoot(relativePath);

        // Відсутній файл не вважаємо помилкою: мета — щоб його не було.
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Складає повний шлях і переконується, що він лишився всередині кореня.
    /// Захист від «../../»: імена ми формуємо самі, але один необережний виклик
    /// у майбутньому не має давати доступу до чужих файлів.
    /// </summary>
    private string ResolveInsideRoot(string relativePath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(root, relativePath));

        if (!fullPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Шлях виходить за межі сховища зображень.");
        }

        return fullPath;
    }
}
