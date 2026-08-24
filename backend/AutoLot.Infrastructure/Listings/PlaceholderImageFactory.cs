using SkiaSharp;

namespace AutoLot.Infrastructure.Listings;

/// <summary>
/// Малює фото-заглушку для демо-оголошень: кольорове тло з назвою марки,
/// моделі та роком (SPEC §11). Справжніх знімків у проєкті немає й не буде,
/// але каталог має виглядати як каталог, а не як список сірих прямокутників.
/// </summary>
internal static class PlaceholderImageFactory
{
    private const int Width = 1200;
    private const int Height = 800;

    /// <summary>
    /// Приглушені кольори: заглушка має читатися як фото-місце, а не
    /// перетягувати увагу на себе.
    /// </summary>
    private static readonly SKColor[] Palette =
    [
        new(0x2F, 0x3E, 0x52),
        new(0x3D, 0x4F, 0x3A),
        new(0x54, 0x3B, 0x3B),
        new(0x3A, 0x45, 0x55),
        new(0x4A, 0x40, 0x2E),
        new(0x33, 0x3A, 0x3F),
    ];

    public static byte[] Create(string make, string model, int year, int photoIndex, int seed)
    {
        using var bitmap = new SKBitmap(Width, Height);
        using var canvas = new SKCanvas(bitmap);

        var background = Palette[Math.Abs(seed) % Palette.Length];

        canvas.Clear(background);
        DrawDiagonalStripes(canvas, background);

        DrawText(canvas, make.ToUpperInvariant(), 64, Height / 2f - 40, SKColors.White);
        DrawText(canvas, $"{model} · {year}", 40, Height / 2f + 30, new SKColor(0xCF, 0xD6, 0xDD));

        // Номер кадру — щоб у галереї було видно, що фото різні.
        DrawText(canvas, $"#{photoIndex + 1}", 28, Height - 60, new SKColor(0x9A, 0xA5, 0xB1));

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 85);

        return data.ToArray();
    }

    /// <summary>
    /// Смуги малюємо поворотом полотна, а не окремими фігурами: так це кілька
    /// прямокутників замість набору вручну складених контурів.
    /// </summary>
    private static void DrawDiagonalStripes(SKCanvas canvas, SKColor background)
    {
        using var paint = new SKPaint
        {
            Color = background.WithAlpha(0x30),
            IsAntialias = true,
        };

        canvas.Save();
        canvas.RotateDegrees(-35, Width / 2f, Height / 2f);

        for (var x = -Height; x < Width + Height; x += 80)
        {
            canvas.DrawRect(x, -Height, 32, Height * 3, paint);
        }

        canvas.Restore();
    }

    private static void DrawText(SKCanvas canvas, string text, float size, float y, SKColor color)
    {
        using var typeface = SKTypeface.FromFamilyName(
            "Segoe UI",
            SKFontStyleWeight.SemiBold,
            SKFontStyleWidth.Normal,
            SKFontStyleSlant.Upright);

        using var font = new SKFont(typeface, size);
        using var paint = new SKPaint { Color = color, IsAntialias = true };

        canvas.DrawText(text, Width / 2f, y, SKTextAlign.Center, font, paint);
    }
}
