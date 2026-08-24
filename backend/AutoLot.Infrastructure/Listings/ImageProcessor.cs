using AutoLot.Application.Listings;
using SkiaSharp;

namespace AutoLot.Infrastructure.Listings;

/// <summary>
/// Готує завантажене зображення до збереження. Три вимоги SPEC §8 закриваються
/// саме тут.
///
/// **Тип файла визначаємо за вмістом, а не за розширенням.** Назвати
/// виконуваний файл «photo.jpg» може будь-хто; декодер дивиться на перші
/// байти, і якщо там не зображення — розбір просто не почнеться.
///
/// **Зображення перекодовуємо, а не зберігаємо як є.** Це не про якість:
/// у JPEG можна сховати EXIF із координатами зйомки, а подекуди й цілий архів
/// усередині файла. Ми малюємо пікселі в новий буфер і кодуємо його заново,
/// тож у результат не потрапляє нічого, крім самого зображення.
/// </summary>
internal static class ImageProcessor
{
    /// <summary>Довша сторона повнорозмірного зображення.</summary>
    private const int MaxDimension = 1920;

    /// <summary>Довша сторона мініатюри для списків.</summary>
    private const int ThumbnailDimension = 400;

    private const int Quality = 82;

    /// <summary>
    /// Занадто велике зображення — теж атака: розпакований у пам'ять
    /// «мільйон на мільйон» пікселів кладе процес незалежно від розміру файла.
    /// </summary>
    private const long MaxPixels = 50_000_000;

    private static readonly HashSet<SKEncodedImageFormat> AllowedFormats =
    [
        SKEncodedImageFormat.Jpeg,
        SKEncodedImageFormat.Png,
        SKEncodedImageFormat.Webp,
    ];

    /// <summary>Повнорозмірна копія й мініатюра, обидві у JPEG.</summary>
    public static async Task<(byte[] Full, byte[] Thumbnail)> ProcessAsync(
        Stream content,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);

        // SkiaSharp працює синхронно, тож копіюємо потік у пам'ять асинхронно,
        // а далі вже не тримаємо потік відкритим.
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;

        using var bitmap = Decode(buffer);

        return (Encode(bitmap, MaxDimension), Encode(bitmap, ThumbnailDimension));
    }

    private static SKBitmap Decode(Stream content)
    {
        using var codec = SKCodec.Create(content)
            ?? throw new ListingDataException("Файл не є зображенням.");

        if (!AllowedFormats.Contains(codec.EncodedFormat))
        {
            throw new ListingDataException("Підтримуються лише JPEG, PNG і WebP.");
        }

        if ((long)codec.Info.Width * codec.Info.Height > MaxPixels)
        {
            throw new ListingDataException("Зображення завелике за роздільною здатністю.");
        }

        var bitmap = SKBitmap.Decode(codec)
            ?? throw new ListingDataException("Файл пошкоджений або не є зображенням.");

        // Знімки з телефона часто «лежать на боці»: правильний поворот
        // записаний лише в EXIF, і після перекодування він зникне. Тому
        // повертаємо самі пікселі, поки ця підказка ще доступна.
        return Reorient(bitmap, codec.EncodedOrigin);
    }

    private static SKBitmap Reorient(SKBitmap bitmap, SKEncodedOrigin origin)
    {
        if (origin is SKEncodedOrigin.Default or SKEncodedOrigin.TopLeft)
        {
            return bitmap;
        }

        // При повороті на 90° сторони міняються місцями.
        var swapSides = origin is SKEncodedOrigin.LeftTop
            or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom
            or SKEncodedOrigin.LeftBottom;

        var width = swapSides ? bitmap.Height : bitmap.Width;
        var height = swapSides ? bitmap.Width : bitmap.Height;

        var rotated = new SKBitmap(width, height);

        using (var canvas = new SKCanvas(rotated))
        {
            canvas.Clear(SKColors.White);

            switch (origin)
            {
                case SKEncodedOrigin.TopRight:
                    canvas.Scale(-1, 1, width / 2f, 0);
                    break;
                case SKEncodedOrigin.BottomRight:
                    canvas.RotateDegrees(180, width / 2f, height / 2f);
                    break;
                case SKEncodedOrigin.BottomLeft:
                    canvas.Scale(1, -1, 0, height / 2f);
                    break;
                case SKEncodedOrigin.LeftTop:
                    canvas.Translate(width, 0);
                    canvas.RotateDegrees(90);
                    canvas.Scale(1, -1, 0, bitmap.Height / 2f);
                    break;
                case SKEncodedOrigin.RightTop:
                    canvas.Translate(width, 0);
                    canvas.RotateDegrees(90);
                    break;
                case SKEncodedOrigin.RightBottom:
                    canvas.Translate(0, height);
                    canvas.RotateDegrees(270);
                    canvas.Scale(1, -1, 0, bitmap.Height / 2f);
                    break;
                case SKEncodedOrigin.LeftBottom:
                    canvas.Translate(0, height);
                    canvas.RotateDegrees(270);
                    break;
                default:
                    break;
            }

            canvas.DrawBitmap(bitmap, 0, 0, new SKSamplingOptions(SKFilterMode.Nearest));
        }

        bitmap.Dispose();

        return rotated;
    }

    private static byte[] Encode(SKBitmap source, int maxDimension)
    {
        var scale = Math.Min(
            1d,
            (double)maxDimension / Math.Max(source.Width, source.Height));

        // Зменшуємо, але ніколи не збільшуємо: розтягнуте фото виглядає гірше
        // за маленьке.
        var width = Math.Max(1, (int)Math.Round(source.Width * scale));
        var height = Math.Max(1, (int)Math.Round(source.Height * scale));

        using var resized = source.Resize(
            new SKImageInfo(width, height),
            new SKSamplingOptions(SKCubicResampler.Mitchell));

        using var image = SKImage.FromBitmap(resized ?? source);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, Quality);

        return data.ToArray();
    }
}
