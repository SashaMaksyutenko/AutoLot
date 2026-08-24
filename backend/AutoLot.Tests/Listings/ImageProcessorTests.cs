using System.Text;
using AutoLot.Application.Listings;
using AutoLot.Infrastructure.Listings;
using SkiaSharp;

namespace AutoLot.Tests.Listings;

/// <summary>
/// Перевіряє обробку завантажених зображень. Тести працюють зі справжніми
/// картинками, згенерованими тут же: підробити результат перекодування
/// заглушкою не вийде.
/// </summary>
public class ImageProcessorTests
{
    [Fact]
    public async Task Re_encodes_png_into_jpeg()
    {
        // Головна вимога SPEC §8: у сховище лягає не той файл, що надіслали,
        // а заново закодовані пікселі — разом із форматом зникає й EXIF.
        var (full, _) = await ImageProcessor.ProcessAsync(Png(800, 600), CancellationToken.None);

        Assert.Equal(SKEncodedImageFormat.Jpeg, FormatOf(full));
    }

    [Fact]
    public async Task Shrinks_a_large_image_to_the_limit()
    {
        var (full, thumbnail) = await ImageProcessor.ProcessAsync(
            Png(4000, 3000),
            CancellationToken.None);

        var fullSize = SizeOf(full);
        var thumbnailSize = SizeOf(thumbnail);

        Assert.Equal(1920, Math.Max(fullSize.Width, fullSize.Height));
        Assert.Equal(400, Math.Max(thumbnailSize.Width, thumbnailSize.Height));
    }

    [Fact]
    public async Task Keeps_the_aspect_ratio()
    {
        var (full, _) = await ImageProcessor.ProcessAsync(Png(4000, 2000), CancellationToken.None);

        var size = SizeOf(full);

        Assert.Equal(1920, size.Width);
        Assert.Equal(960, size.Height);
    }

    [Fact]
    public async Task Does_not_enlarge_a_small_image()
    {
        // Розтягнуте фото виглядає гірше за маленьке, тож угору не масштабуємо.
        var (full, _) = await ImageProcessor.ProcessAsync(Png(320, 240), CancellationToken.None);

        var size = SizeOf(full);

        Assert.Equal(320, size.Width);
        Assert.Equal(240, size.Height);
    }

    [Fact]
    public async Task Rejects_a_file_that_only_pretends_to_be_an_image()
    {
        // Саме цей випадок ловить перевірка за вмістом: розширення каже «.jpg»,
        // а всередині щось інше.
        var content = new MemoryStream(Encoding.UTF8.GetBytes("MZ не зображення, а виконуваний файл"));

        await Assert.ThrowsAsync<ListingDataException>(
            () => ImageProcessor.ProcessAsync(content, CancellationToken.None));
    }

    [Fact]
    public async Task Rejects_an_empty_file()
    {
        await Assert.ThrowsAsync<ListingDataException>(
            () => ImageProcessor.ProcessAsync(new MemoryStream(), CancellationToken.None));
    }

    /// <summary>Малює картинку з градієнтом — суцільна заливка стискається надто добре.</summary>
    private static MemoryStream Png(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        using (var paint = new SKPaint())
        {
            paint.Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0),
                new SKPoint(width, height),
                [SKColors.OrangeRed, SKColors.MidnightBlue],
                SKShaderTileMode.Clamp);

            canvas.DrawRect(0, 0, width, height, paint);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);

        return new MemoryStream(data.ToArray());
    }

    private static SKEncodedImageFormat FormatOf(byte[] content)
    {
        using var codec = SKCodec.Create(new MemoryStream(content));

        return codec.EncodedFormat;
    }

    private static SKSizeI SizeOf(byte[] content)
    {
        using var codec = SKCodec.Create(new MemoryStream(content));

        return new SKSizeI(codec.Info.Width, codec.Info.Height);
    }
}
