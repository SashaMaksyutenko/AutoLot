using AutoLot.Infrastructure.Listings;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;

namespace AutoLot.Api.Extensions;

public static class PhotoServing
{
    /// <summary>
    /// Роздає завантажені зображення за адресою /media. Тека сховища навмисно
    /// лежить поза wwwroot: туди не має потрапляти нічого, крім того, що ми
    /// самі згенерували.
    /// </summary>
    public static WebApplication UseListingPhotos(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var root = Path.GetFullPath(
            app.Configuration[$"{PhotoStorageOptions.SectionName}:RootPath"] ?? "uploads");

        Directory.CreateDirectory(root);

        // Білий список типів: ми зберігаємо тільки JPEG, тож усе інше в цій
        // теці — або помилка, або чужа спроба, і віддавати його не треба.
        var contentTypes = new FileExtensionContentTypeProvider(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [".jpg"] = "image/jpeg",
            });

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(root),
            RequestPath = "/media",
            ContentTypeProvider = contentTypes,
            ServeUnknownFileTypes = false,

            // Зображення незмінні: ім'я файла унікальне, тож нова версія — це
            // завжди новий шлях, і кешувати можна надовго.
            OnPrepareResponse = context =>
                context.Context.Response.Headers.CacheControl = "public,max-age=31536000,immutable",
        });

        return app;
    }
}
