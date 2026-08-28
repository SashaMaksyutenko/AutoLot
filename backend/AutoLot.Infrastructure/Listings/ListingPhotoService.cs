using AutoLot.Application.Common.Abstractions;
using AutoLot.Application.Listings;
using AutoLot.Application.Listings.Dtos;
using AutoLot.Domain.Listings;
using AutoLot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AutoLot.Infrastructure.Listings;

internal sealed class ListingPhotoService(
    AutoLotDbContext dbContext,
    IPhotoStorage storage,
    IOptions<PhotoStorageOptions> options,
    ListingAccess access) : IListingPhotoService
{
    private readonly PhotoStorageOptions settings = options.Value;

    public async Task<IReadOnlyList<ListingPhoto>> GetAsync(
        long listingId,
        long actorId,
        CancellationToken cancellationToken = default)
    {
        var car = await LoadCarAsync(listingId, actorId, cancellationToken);

        return Map(car.Photos);
    }

    public async Task<ListingPhoto> AddAsync(
        long listingId,
        long actorId,
        PhotoUpload upload,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(upload);

        var car = await LoadCarAsync(listingId, actorId, cancellationToken);

        if (car.Photos.Count >= settings.MaxPhotosPerListing)
        {
            throw new Domain.Common.DomainRuleException(
                $"До оголошення можна додати не більше {settings.MaxPhotosPerListing} фото.");
        }

        if (upload.Length > settings.MaxFileSizeBytes)
        {
            throw new ListingDataException(
                $"Файл завеликий: максимум {settings.MaxFileSizeBytes / (1024 * 1024)} МБ.");
        }

        var (full, thumbnail) = await ImageProcessor.ProcessAsync(upload.Content, cancellationToken);

        // Ім'я генеруємо самі й ігноруємо надіслане: у ньому можуть бути
        // і «../», і кирилиця, і збіг з наявним файлом.
        var directory = $"listings/{listingId}";
        var name = Guid.CreateVersion7().ToString("n");

        var photo = new CarPhoto
        {
            CarId = car.Id,
            Path = await storage.SaveAsync(directory, $"{name}.jpg", full, cancellationToken),
            ThumbnailPath = await storage.SaveAsync(directory, $"{name}-thumb.jpg", thumbnail, cancellationToken),
            SortOrder = car.Photos.Count == 0 ? 0 : car.Photos.Max(item => item.SortOrder) + 1,

            // Перше фото стає головним автоматично — інакше оголошення
            // потрапило б у видачу без картинки.
            IsPrimary = car.Photos.Count == 0,
        };

        car.Photos.Add(photo);

        await dbContext.SaveChangesAsync(cancellationToken);

        return Map(photo);
    }

    public async Task DeleteAsync(
        long listingId,
        long actorId,
        long photoId,
        CancellationToken cancellationToken = default)
    {
        var car = await LoadCarAsync(listingId, actorId, cancellationToken);

        var photo = car.Photos.FirstOrDefault(item => item.Id == photoId)
            ?? throw new ListingNotFoundException(photoId);

        car.Photos.Remove(photo);

        // Головне фото зникло — призначаємо наступне, щоб картка не лишилася
        // без зображення.
        if (photo.IsPrimary)
        {
            var replacement = car.Photos.OrderBy(item => item.SortOrder).FirstOrDefault();

            if (replacement is not null)
            {
                replacement.IsPrimary = true;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        // Файли прибираємо після успішного збереження: якщо база відкотиться,
        // краще лишити зайвий файл, ніж запис без файла.
        await storage.DeleteAsync(photo.Path, cancellationToken);
        await storage.DeleteAsync(photo.ThumbnailPath, cancellationToken);
    }

    public async Task ReorderAsync(
        long listingId,
        long actorId,
        IReadOnlyList<long> photoIdsInOrder,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(photoIdsInOrder);

        var car = await LoadCarAsync(listingId, actorId, cancellationToken);

        var known = car.Photos.Select(photo => photo.Id).ToHashSet();

        // Вимагаємо повний перелік: часткове перевпорядкування лишало б
        // фото з однаковим порядком, і видача малювала б їх як пощастить.
        if (photoIdsInOrder.Count != known.Count || !photoIdsInOrder.All(known.Contains))
        {
            throw new ListingDataException("Перелік має містити всі фото оголошення рівно по разу.");
        }

        for (var index = 0; index < photoIdsInOrder.Count; index++)
        {
            car.Photos.First(photo => photo.Id == photoIdsInOrder[index]).SortOrder = index;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SetPrimaryAsync(
        long listingId,
        long actorId,
        long photoId,
        CancellationToken cancellationToken = default)
    {
        var car = await LoadCarAsync(listingId, actorId, cancellationToken);

        if (car.Photos.All(photo => photo.Id != photoId))
        {
            throw new ListingNotFoundException(photoId);
        }

        foreach (var photo in car.Photos)
        {
            photo.IsPrimary = photo.Id == photoId;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Дістає авто разом із фото й одразу перевіряє право діяти. Усі методи
    /// починаються з нього, тож забути перевірку не вийде.
    /// </summary>
    private async Task<Car> LoadCarAsync(
        long listingId,
        long actorId,
        CancellationToken cancellationToken)
    {
        var listing = await dbContext.Listings
            .Include(item => item.Car).ThenInclude(car => car.Photos)
            .FirstOrDefaultAsync(item => item.Id == listingId, cancellationToken)
            ?? throw new ListingNotFoundException(listingId);

        if (!await access.CanManageAsync(listing, actorId, cancellationToken))
        {
            throw new ListingAccessException("Це оголошення належить іншому продавцеві.");
        }

        return listing.Car;
    }

    private static IReadOnlyList<ListingPhoto> Map(IEnumerable<CarPhoto> photos) =>
        [.. photos.OrderBy(photo => photo.SortOrder).Select(Map)];

    private static ListingPhoto Map(CarPhoto photo) =>
        new(photo.Id, photo.Path, photo.ThumbnailPath, photo.SortOrder, photo.IsPrimary);
}
